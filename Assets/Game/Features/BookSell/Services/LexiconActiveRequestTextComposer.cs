using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Configs;
using Game.Configs.Models;
using Game.Localization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Composes the request line from four slots — opener, core, constraint, exclusion, anchor — by looking
    /// every condition leaf up in the phrase lexicon (request_phrases.json) and stitching the fragments
    /// together. The lexicon holds localization keys only; phrases live in localization_quests_en.json.
    ///
    /// <para>Two deliberate properties:</para>
    /// <list type="bullet">
    /// <item><description>Not every condition is voiced. How much is said depends on how many books in the
    /// catalogue can satisfy the request at all — a request only two books answer must spell out every
    /// constraint, one thirty-seven books answer would read like a checklist if it did.</description></item>
    /// <item><description>Opener and anchor are picked deterministically from the request id, so a request
    /// always reads the same way. Nothing re-rolls when the day is respawned after a reload.</description></item>
    /// </list>
    ///
    /// <para>See docs/INPROGRESS/ACTIVE_REQUEST_TEXT_COMPOSER.md for the authored lexicon and the dry run.</para>
    /// </summary>
    public sealed class LexiconActiveRequestTextComposer : IActiveRequestTextComposer
    {
        private const string LogPrefix = "[ActiveRequests]";

        private const int TightMax = 5;
        private const int MediumMax = 15;

        private const string KindTerm = "term";
        private const string KindBand = "band";
        private const string KindCombo = "combo";
        private const string KindOpener = "opener";
        private const string KindLead = "lead";
        private const string KindAnchor = "anchor";

        private readonly IConfigsService _configs;
        private readonly IBookConditionRequestEvaluator _evaluator;
        private readonly ILocalizationService _localization;
        private readonly Dictionary<string, Verbosity> _verbosityCache = new(StringComparer.Ordinal);

        private Lexicon _lexicon;

        /// <summary>Injected service in the game, the locator in cheat/editor paths that run outside the sales scope.</summary>
        private ILocalizationService Localization => _localization ?? LocalizationLocator.Service;

        public LexiconActiveRequestTextComposer(
            IConfigsService configs,
            IBookConditionRequestEvaluator evaluator,
            ILocalizationService localization)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _localization = localization;
        }

        public string Compose(RequestDefinitionConfig request)
        {
            if (request == null) return string.Empty;

            // An authored override wins outright: it exists for the requests the lexicon cannot phrase well.
            if (!string.IsNullOrWhiteSpace(request.DescriptionKey) &&
                TryLocalize(request.DescriptionKey, out var authored))
                return authored;

            var lexicon = EnsureLexicon();
            var fragments = new List<Fragment>();

            // The `all` group is authored in whatever order the designer typed it, but the sentence has to
            // open on the genre — "I want with a detective in it, a classic" is not a sentence.
            var mandatory = new List<Fragment>();
            CollectGroup(request.Conditions?.All, wantPositive: true, joinWithOr: false, lexicon, request.Id, mandatory);
            AppendByRank(mandatory, fragments);

            CollectGroup(request.Conditions?.Any, wantPositive: true, joinWithOr: true, lexicon, request.Id, fragments);
            CollectGroup(request.Conditions?.None, wantPositive: false, joinWithOr: false, lexicon, request.Id, fragments);

            ApplyCombos(lexicon, fragments);
            FilterByVerbosity(request, fragments);

            return Assemble(request, lexicon, fragments);
        }

        // ---------- assembly ----------

        private string Assemble(RequestDefinitionConfig request, Lexicon lexicon, List<Fragment> fragments)
        {
            var body = new StringBuilder();
            for (var i = 0; i < fragments.Count; i++)
            {
                if (body.Length > 0) body.Append(", ");
                body.Append(fragments[i].Text);
            }

            var anchor = ResolveAnchor(request, lexicon);

            if (body.Length == 0)
            {
                // Every fragment was dropped — a request whose conditions the lexicon does not cover.
                Debug.LogWarning($"{LogPrefix} request '{request.Id}' produced no phrase fragments; " +
                                 "falling back to the anchor sentence alone.");
                return string.IsNullOrEmpty(anchor) ? request.Id : anchor;
            }

            var text = new StringBuilder();

            var opener = Pick(lexicon.Openers, request.Id, KindOpener);
            if (!string.IsNullOrWhiteSpace(opener)) text.Append(opener).Append(' ');

            var lead = Pick(lexicon.Leads, request.Id, KindLead);
            if (!string.IsNullOrWhiteSpace(lead)) text.Append(lead).Append(' ');

            text.Append(body).Append('.');

            if (!string.IsNullOrEmpty(anchor)) text.Append(' ').Append(anchor);

            return text.ToString();
        }

        private string ResolveAnchor(RequestDefinitionConfig request, Lexicon lexicon)
        {
            if (string.IsNullOrWhiteSpace(request.BookTitle)) return string.Empty;
            if (lexicon.Anchors.Count == 0) return string.Empty;

            var entry = lexicon.Anchors[StableIndex(request.Id, KindAnchor, lexicon.Anchors.Count)];
            if (entry?.PositiveKey == null) return string.Empty;

            var localization = Localization;
            return localization != null ? localization.Get(entry.PositiveKey, request.BookTitle) : string.Empty;
        }

        private string Pick(IReadOnlyList<RequestPhraseConfig> pool, string requestId, string salt)
        {
            if (pool == null || pool.Count == 0) return string.Empty;
            var entry = pool[StableIndex(requestId, salt, pool.Count)];
            return entry != null && TryLocalize(entry.PositiveKey, out var text) ? text : string.Empty;
        }

        // ---------- fragments ----------

        private void CollectGroup(
            RequestCondition[] group,
            bool wantPositive,
            bool joinWithOr,
            Lexicon lexicon,
            string requestId,
            List<Fragment> sink)
        {
            if (group == null || group.Length == 0) return;

            var groupFragments = joinWithOr ? new List<Fragment>() : sink;

            for (var i = 0; i < group.Length; i++)
                CollectLeaf(group[i], wantPositive, lexicon, requestId, groupFragments);

            if (!joinWithOr || groupFragments.Count == 0) return;

            // An `any` group is an OR over the whole group, so it reads as one clause.
            sink.Add(new Fragment(null, JoinOr(groupFragments), IsAllBands(groupFragments), MinRank(groupFragments)));
        }

        private void CollectLeaf(
            RequestCondition condition,
            bool wantPositive,
            Lexicon lexicon,
            string requestId,
            List<Fragment> sink)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.Type)) return;

            if (IsListType(condition.Type))
            {
                CollectTermLeaf(condition, wantPositive, lexicon, requestId, sink);
                return;
            }

            // Numeric bands read as a soft range ("a real brick"); there is no negated form of one, so a
            // numeric condition inside a `none` group simply goes unspoken.
            if (!wantPositive)
            {
                Debug.LogWarning($"{LogPrefix} request '{requestId}': numeric condition " +
                                 $"'{condition.Type} {condition.Operator}' cannot be voiced inside a none group.");
                return;
            }

            var band = lexicon.FindBand(condition);
            if (band == null)
            {
                WarnMissing(requestId, condition);
                return;
            }

            if (TryLocalize(band.PositiveKey, out var text))
                sink.Add(new Fragment(band.Id, text, isBand: true, Fragment.BandRank));
        }

        private void CollectTermLeaf(
            RequestCondition condition,
            bool wantPositive,
            Lexicon lexicon,
            string requestId,
            List<Fragment> sink)
        {
            var op = condition.Operator ?? string.Empty;
            var values = ReadValues(condition.Value);
            if (values.Count == 0) return;

            // notContains / containsNone flip the sense of the leaf relative to the group it sits in.
            var positive = wantPositive;
            if (op.Equals("notEqual", StringComparison.OrdinalIgnoreCase) ||
                op.Equals("notContains", StringComparison.OrdinalIgnoreCase) ||
                op.Equals("containsNone", StringComparison.OrdinalIgnoreCase))
                positive = !positive;

            var rank = string.Equals(condition.Type, "genres", StringComparison.OrdinalIgnoreCase)
                ? Fragment.GenreRank
                : Fragment.QualityRank;

            var leafFragments = new List<Fragment>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var term = lexicon.FindTerm(condition.Type, values[i]);
                if (term == null)
                {
                    WarnMissing(requestId, condition, values[i]);
                    continue;
                }

                var key = positive ? term.PositiveKey : term.NegativeKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    // Deliberate gap in the lexicon, not a typo: some exclusions cannot be voiced politely.
                    Debug.LogWarning($"{LogPrefix} request '{requestId}': term '{values[i]}' has no " +
                                     $"{(positive ? "positive" : "negative")} phrase and is left unspoken.");
                    continue;
                }

                if (TryLocalize(key, out var text))
                    leafFragments.Add(new Fragment(term.Id, text, isBand: false, rank));
            }

            if (leafFragments.Count == 0) return;

            // containsAny is an OR *inside* one leaf and must read as one clause; the rest are separate.
            if (op.Equals("containsAny", StringComparison.OrdinalIgnoreCase) && leafFragments.Count > 1)
            {
                sink.Add(new Fragment(null, JoinOr(leafFragments), isBand: false, rank));
                return;
            }

            sink.AddRange(leafFragments);
        }

        private void ApplyCombos(Lexicon lexicon, List<Fragment> fragments)
        {
            for (var c = 0; c < lexicon.Combos.Count; c++)
            {
                var combo = lexicon.Combos[c];
                if (combo?.Terms == null || combo.Terms.Length == 0) continue;

                var indices = new List<int>(combo.Terms.Length);
                for (var t = 0; t < combo.Terms.Length; t++)
                {
                    var index = IndexOfFragment(fragments, combo.Terms[t]);
                    if (index < 0) { indices.Clear(); break; }
                    indices.Add(index);
                }

                if (indices.Count != combo.Terms.Length) continue;
                if (!TryLocalize(combo.PositiveKey, out var text)) continue;

                indices.Sort();
                var head = indices[0];
                for (var i = indices.Count - 1; i >= 1; i--)
                    fragments.RemoveAt(indices[i]);

                fragments[head] = new Fragment(combo.Id, text, isBand: false, fragments[head].Rank);
            }
        }

        private void FilterByVerbosity(RequestDefinitionConfig request, List<Fragment> fragments)
        {
            var verbosity = ResolveVerbosity(request);
            if (verbosity == Verbosity.Tight) return;

            for (var i = fragments.Count - 1; i >= 0; i--)
            {
                if (!fragments[i].IsBand) continue;
                if (verbosity == Verbosity.Loose) { fragments.RemoveAt(i); continue; }

                // Medium: a `between` band is narrow enough to be worth saying; an open-ended one is not.
                if (!IsBetweenBand(fragments[i].Id)) fragments.RemoveAt(i);
            }
        }

        private bool IsBetweenBand(string bandId)
        {
            if (string.IsNullOrEmpty(bandId)) return false;
            return _lexicon != null &&
                   _lexicon.BandsById.TryGetValue(bandId, out var band) &&
                   string.Equals(band.Operator, "between", StringComparison.OrdinalIgnoreCase);
        }

        // ---------- verbosity ----------

        private Verbosity ResolveVerbosity(RequestDefinitionConfig request)
        {
            if (string.IsNullOrEmpty(request.Id)) return Verbosity.Medium;
            if (_verbosityCache.TryGetValue(request.Id, out var cached)) return cached;

            var books = _configs.GetAll<BookConfig>();
            var matches = 0;
            for (var i = 0; i < books.Count; i++)
            {
                var book = books[i];
                if (book != null && _evaluator.Evaluate(book, request).IsMatch) matches++;
            }

            var verbosity = matches <= TightMax
                ? Verbosity.Tight
                : matches <= MediumMax
                    ? Verbosity.Medium
                    : Verbosity.Loose;

            _verbosityCache[request.Id] = verbosity;
            return verbosity;
        }

        // ---------- lexicon ----------

        private Lexicon EnsureLexicon()
        {
            if (_lexicon != null) return _lexicon;

            var lexicon = Lexicon.Build(_configs.GetAll<RequestPhraseConfig>());
            if (lexicon.IsEmpty)
            {
                // Not cached: configs may simply not be warmed up yet, and an empty lexicon would stick.
                Debug.LogError($"{LogPrefix} phrase lexicon is empty - request_phrases.json is missing " +
                               "or failed to load; request text falls back to the anchor sentence alone.");
                return lexicon;
            }

            _lexicon = lexicon;
            return _lexicon;
        }

        private bool TryLocalize(string key, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var localization = Localization;
            if (localization == null) return false;

            if (!localization.TryGet(key, out text) || text == null)
            {
                Debug.LogWarning($"{LogPrefix} missing localization key '{key}' for a request phrase.");
                text = string.Empty;
                return false;
            }

            // An empty value is a legitimate entry (the "no opener" slot), just not a fragment.
            return !string.IsNullOrEmpty(text);
        }

        private static void WarnMissing(string requestId, RequestCondition condition, string value = null)
        {
            var payload = value ?? FormatValue(condition.Value);
            Debug.LogWarning($"{LogPrefix} request '{requestId}': no lexicon entry for " +
                             $"'{condition.Type} {condition.Operator} {payload}'; the fragment is left unspoken.");
        }

        // ---------- helpers ----------

        private static string JoinOr(List<Fragment> fragments)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < fragments.Count; i++)
            {
                if (i > 0) sb.Append(i == fragments.Count - 1 ? " or " : ", ");
                sb.Append(fragments[i].Text);
            }
            return sb.ToString();
        }

        /// <summary>Stable reorder: genre first, then qualities, then the numeric band.</summary>
        private static void AppendByRank(List<Fragment> source, List<Fragment> sink)
        {
            for (var rank = Fragment.GenreRank; rank <= Fragment.BandRank; rank++)
                for (var i = 0; i < source.Count; i++)
                    if (source[i].Rank == rank)
                        sink.Add(source[i]);
        }

        private static int MinRank(List<Fragment> fragments)
        {
            var min = Fragment.BandRank;
            for (var i = 0; i < fragments.Count; i++)
                if (fragments[i].Rank < min) min = fragments[i].Rank;
            return min;
        }

        private static bool IsAllBands(List<Fragment> fragments)
        {
            for (var i = 0; i < fragments.Count; i++)
                if (!fragments[i].IsBand) return false;
            return fragments.Count > 0;
        }

        private static int IndexOfFragment(List<Fragment> fragments, string id)
        {
            for (var i = 0; i < fragments.Count; i++)
                if (string.Equals(fragments[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static List<string> ReadValues(JToken value)
        {
            var values = new List<string>();
            if (value == null || value.Type == JTokenType.Null) return values;

            if (value.Type == JTokenType.Array)
            {
                foreach (var item in value)
                {
                    var text = item?.Type == JTokenType.String ? item.Value<string>() : item?.ToString();
                    if (!string.IsNullOrWhiteSpace(text)) values.Add(text);
                }
                return values;
            }

            var scalar = value.Type == JTokenType.String ? value.Value<string>() : value.ToString();
            if (!string.IsNullOrWhiteSpace(scalar)) values.Add(scalar);
            return values;
        }

        private static string FormatValue(JToken value)
            => value == null ? "<null>" : value.ToString(Newtonsoft.Json.Formatting.None);

        private static bool IsListType(string type)
            => string.Equals(type, "genres", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "qualities", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// FNV-1a over the request id. A hand-rolled hash on purpose: <c>string.GetHashCode</c> is not
        /// stable across runtimes, and the whole point is that a request reads the same way every time.
        /// </summary>
        private static int StableIndex(string id, string salt, int count)
        {
            if (count <= 1) return 0;

            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;

                var hash = offsetBasis;
                if (id != null)
                    for (var i = 0; i < id.Length; i++) { hash ^= id[i]; hash *= prime; }
                for (var i = 0; i < salt.Length; i++) { hash ^= salt[i]; hash *= prime; }

                return (int)(hash % (uint)count);
            }
        }

        private readonly struct Fragment
        {
            public const int GenreRank = 0;
            public const int QualityRank = 1;
            public const int BandRank = 2;

            public Fragment(string id, string text, bool isBand, int rank)
            {
                Id = id;
                Text = text;
                IsBand = isBand;
                Rank = rank;
            }

            public string Id { get; }
            public string Text { get; }
            public bool IsBand { get; }

            /// <summary>Reading order inside a group: genre, then qualities, then the numeric band.</summary>
            public int Rank { get; }
        }

        private enum Verbosity
        {
            Tight,
            Medium,
            Loose
        }

        private sealed class Lexicon
        {
            private readonly Dictionary<string, RequestPhraseConfig> _terms = new(StringComparer.Ordinal);
            private readonly Dictionary<string, RequestPhraseConfig> _bands = new(StringComparer.Ordinal);

            public Dictionary<string, RequestPhraseConfig> BandsById { get; } = new(StringComparer.Ordinal);
            public List<RequestPhraseConfig> Combos { get; } = new();
            public List<RequestPhraseConfig> Openers { get; } = new();
            public List<RequestPhraseConfig> Leads { get; } = new();
            public List<RequestPhraseConfig> Anchors { get; } = new();

            public bool IsEmpty => _terms.Count == 0 && _bands.Count == 0;

            public static Lexicon Build(IReadOnlyList<RequestPhraseConfig> entries)
            {
                var lexicon = new Lexicon();
                if (entries == null) return lexicon;

                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;

                    switch (entry.Kind)
                    {
                        case KindTerm:
                            lexicon._terms[TermKey(entry.Type, ReadScalar(entry.Value))] = entry;
                            break;
                        case KindBand:
                            lexicon._bands[BandKey(entry.Type, entry.Operator, ReadScalar(entry.Value), entry.Min, entry.Max)] = entry;
                            lexicon.BandsById[entry.Id] = entry;
                            break;
                        case KindCombo: lexicon.Combos.Add(entry); break;
                        case KindOpener: lexicon.Openers.Add(entry); break;
                        case KindLead: lexicon.Leads.Add(entry); break;
                        case KindAnchor: lexicon.Anchors.Add(entry); break;
                        default:
                            Debug.LogWarning($"{LogPrefix} phrase '{entry.Id}' has unknown kind '{entry.Kind}' and is ignored.");
                            break;
                    }
                }

                // GetAll order follows a dictionary and is not stable; the deterministic pick needs it to be.
                lexicon.Combos.Sort(ById);
                lexicon.Openers.Sort(ById);
                lexicon.Leads.Sort(ById);
                lexicon.Anchors.Sort(ById);
                return lexicon;
            }

            public RequestPhraseConfig FindTerm(string type, string value)
                => _terms.TryGetValue(TermKey(type, value), out var entry) ? entry : null;

            public RequestPhraseConfig FindBand(RequestCondition condition)
            {
                var isBetween = string.Equals(condition.Operator, "between", StringComparison.OrdinalIgnoreCase);
                double? min = null, max = null;
                string scalar = null;

                if (isBetween)
                {
                    min = ReadNumber(condition.Value?["min"]);
                    max = ReadNumber(condition.Value?["max"]);
                }
                else
                {
                    scalar = ReadScalar(condition.Value);
                }

                return _bands.TryGetValue(BandKey(condition.Type, condition.Operator, scalar, min, max), out var entry)
                    ? entry
                    : null;
            }

            private static int ById(RequestPhraseConfig a, RequestPhraseConfig b)
                => string.CompareOrdinal(a?.Id, b?.Id);

            private static string TermKey(string type, string value)
                => $"{Normalize(type)}|{Normalize(value)}";

            private static string BandKey(string type, string op, string scalar, double? min, double? max)
                => $"{Normalize(type)}|{Normalize(op)}|{Num(scalar)}|{Num(min)}|{Num(max)}";

            private static string Num(double? value)
                => value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;

            private static string Num(string scalar)
                => double.TryParse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    ? Num((double?)value)
                    : string.Empty;

            private static double? ReadNumber(JToken token)
                => token == null || token.Type == JTokenType.Null ? null : token.Value<double?>();

            private static string ReadScalar(JToken token)
            {
                if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Array) return null;
                return token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            }

            /// <summary>Matches the vocabulary normalisation in docs/ACTIVE_REQUEST_CONDITIONS.md: hyphen and space are one separator.</summary>
            private static string Normalize(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;

                var sb = new StringBuilder(value.Length);
                var lastWasSpace = false;
                foreach (var c in value.Trim())
                {
                    var current = c == '-' ? ' ' : char.ToLowerInvariant(c);
                    if (current == ' ')
                    {
                        if (lastWasSpace) continue;
                        lastWasSpace = true;
                    }
                    else
                    {
                        lastWasSpace = false;
                    }

                    sb.Append(current);
                }

                return sb.ToString();
            }
        }
    }
}
