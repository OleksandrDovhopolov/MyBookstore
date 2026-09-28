using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Checks the rewritten book descriptions in docs/content/book_descriptions.json before they replace the
    /// live text. The live catalogue text came from an external spreadsheet and is being rewritten from
    /// scratch (CONTENT-1 in docs/RELEASE_TASKS.md; the writing rules are in
    /// docs/content/BOOK_DESCRIPTION_STYLE.md and this class is authoritative for the numbers in it).
    ///
    /// <list type="bullet">
    /// <item><description><b>Errors</b> — the draft is wrong as data: unknown, duplicate or excluded book, an
    /// anchor that no longer matches the catalogue, markup the renderer would eat, first person, or text that
    /// still is the text being replaced.</description></item>
    /// <item><description><b>Warnings</b> — the draft may be wrong as writing: unusual length, wording that
    /// overlaps the seeded corpus, a year the catalogue disagrees with, an unrecognized book. These need a
    /// human, not a gate.</description></item>
    /// </list>
    ///
    /// <para>Comparison is against the frozen seed archive, never against live localization: once a batch is
    /// applied the live value IS the new text, and every applied row would report itself as unchanged.
    /// Live localization is used for two other things — the title/author anchors (those are not being
    /// rewritten) and verifying that an <c>applied</c> row actually reached the game.</para>
    ///
    /// <para>Reads the json files directly: there is no <c>IConfigsService</c> outside Play mode.</para>
    /// </summary>
    public static class BookDescriptionDraftValidator
    {
        public const string ConfigsDir = "Assets/Configs";
        public const string SeedPath = "docs/content/book_descriptions_seed_en.json";
        public const string DraftsPath = "docs/content/book_descriptions.json";
        public const string BooksFileName = "books.json";
        public const string LocalizationFileName = "localization_books_en.json";

        /// <summary>
        /// Editorial target, not a UI limit: nothing in code truncates a description, the only render site is
        /// RecommendationMinigameWindow's DetailDescription. The window was originally taken from the corpus
        /// being deleted, so it is a soft guide until someone measures the tallest card in the prefab.
        /// </summary>
        public const int MinReasonableLength = 130;
        public const int MaxReasonableLength = 265;

        /// <summary>
        /// Shared runs shorter than this are ordinary English ("a collection of short stories about").
        /// Title and author are masked out before counting, so a description naming its own book is fine.
        /// </summary>
        public const int OverlapWindowWords = 8;

        /// <summary>TMP parses these as rich-text tags and would swallow part of the sentence.</summary>
        private static readonly char[] MarkupChars = { '{', '}', '<', '>' };

        private static readonly char[] WordSeparators =
        {
            ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '(', ')', '-', '\''
        };

        /// <summary>
        /// Lower-case first-person markers. "I" is matched case-sensitively and "us" lower-case only, so
        /// "the US" and an initial in a name do not trip an error-level rule.
        /// </summary>
        private static readonly HashSet<string> FirstPersonWords = new(StringComparer.Ordinal)
        {
            "my", "mine", "we", "our", "ours", "us", "me", "ourselves"
        };

        private static readonly Regex YearPattern = new(@"\b(1[0-9]{3}|20[0-9]{2})\b", RegexOptions.Compiled);

        public static BookDescriptionDraftReport Validate(
            string configsDir = ConfigsDir,
            string seedPath = SeedPath,
            string draftsPath = DraftsPath)
        {
            var report = new BookDescriptionDraftReport { DraftsPath = draftsPath };

            if (!TryLoad<BookDescriptionDraft>(draftsPath, report, out var drafts)) return report;
            if (!TryLoad<BookConfig>(Path.Combine(configsDir, BooksFileName).Replace('\\', '/'), report, out var books))
                return report;

            var seed = LoadSeed(seedPath, report);
            if (seed == null) return report;

            var localization = LoadLocalization(
                Path.Combine(configsDir, LocalizationFileName).Replace('\\', '/'), report);
            if (localization == null) return report;

            var booksById = new Dictionary<string, BookConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var book in books)
            {
                if (book?.Id != null)
                    booksById[book.Id] = book;
            }

            var corpus = BuildCorpusIndex(seed);
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var draft in drafts)
            {
                if (draft == null)
                {
                    report.Errors.Add("Drafts file contains a null entry.");
                    continue;
                }

                report.CheckedDrafts++;
                ValidateDraft(draft, booksById, seed, localization, corpus, seenIds, report);
            }

            return report;
        }

        private static void ValidateDraft(
            BookDescriptionDraft draft,
            IReadOnlyDictionary<string, BookConfig> booksById,
            IReadOnlyDictionary<string, SeedEntry> seed,
            IReadOnlyDictionary<string, string> localization,
            IReadOnlyDictionary<string, List<string>> corpus,
            HashSet<string> seenIds,
            BookDescriptionDraftReport report)
        {
            var id = draft.Id;
            var errorsBefore = report.Errors.Count;

            if (string.IsNullOrWhiteSpace(id))
            {
                report.Errors.Add("A draft has no 'id'.");
                return;
            }

            if (!seenIds.Add(id))
            {
                report.Errors.Add($"'{id}' is drafted more than once.");
                return;
            }

            if (!booksById.TryGetValue(id, out var book))
            {
                report.Errors.Add($"'{id}' is not a book in {BooksFileName}.");
                return;
            }

            if (!seed.TryGetValue(id, out var seedEntry))
            {
                report.Errors.Add($"'{id}' has no row in the seed archive — the archive and the catalogue disagree.");
                return;
            }

            // Excluded books are not this game's content; rewriting them would be wasted work that also
            // never shows up in the game (see BookConfig.IsExcludedFromCatalog).
            if (book.IsExcludedFromCatalog)
                report.Errors.Add($"'{id}' is excluded from the catalogue and must not be rewritten.");

            if (!BookDescriptionDraft.IsAllowedStatus(draft.Status))
            {
                report.Errors.Add(
                    $"'{id}' has status '{draft.Status}'; expected one of " +
                    $"{string.Join(", ", BookDescriptionDraft.AllowedStatuses)}.");
            }

            if (draft.Batch <= 0)
                report.Errors.Add($"'{id}' has no positive 'batch'.");
            else
                report.Batches.Add(draft.Batch);

            var title = Lookup(localization, book.TitleKey);
            var author = Lookup(localization, book.AuthorKey);
            ValidateAnchors(draft, title, author, report);

            var text = draft.New;
            if (string.IsNullOrWhiteSpace(text))
            {
                report.Errors.Add($"'{id}' has an empty 'new' description.");
                return;
            }

            var masked = new[] { title, author };

            ValidateFormatting(id, text, report);
            ValidateFirstPerson(id, text, masked, report);
            ValidateAgainstSeed(draft, seedEntry, masked, report);
            ValidateAgainstCorpus(draft, corpus, masked, report);
            ValidateLength(id, text, report);
            ValidateFacts(draft, book, report);
            ValidateApplied(draft, book, localization, report);

            if (report.Errors.Count == errorsBefore)
                report.Ready++;
        }

        /// <summary>
        /// Book ids are positional in the seed spreadsheet, so a shifted row silently moves a draft onto
        /// another book. Comparing the title and author recorded at draft time against the catalogue catches it.
        /// </summary>
        private static void ValidateAnchors(
            BookDescriptionDraft draft,
            string title,
            string author,
            BookDescriptionDraftReport report)
        {
            if (!AnchorMatches(draft.TitleAtDraft, title))
            {
                report.Errors.Add(
                    $"'{draft.Id}' was drafted for title '{draft.TitleAtDraft}' but the catalogue now says " +
                    $"'{title}' — the draft may have shifted onto another book.");
            }

            if (!AnchorMatches(draft.AuthorAtDraft, author))
            {
                report.Errors.Add(
                    $"'{draft.Id}' was drafted for author '{draft.AuthorAtDraft}' but the catalogue now says " +
                    $"'{author}'.");
            }
        }

        private static void ValidateFormatting(string id, string text, BookDescriptionDraftReport report)
        {
            if (text != text.Trim())
                report.Errors.Add($"'{id}' has leading or trailing whitespace.");

            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0 || text.IndexOf('\t') >= 0)
                report.Errors.Add($"'{id}' contains a line break or tab; descriptions are single-paragraph.");

            if (text.Contains("  "))
                report.Errors.Add($"'{id}' contains a double space.");

            var markup = text.IndexOfAny(MarkupChars);
            if (markup >= 0)
            {
                report.Errors.Add(
                    $"'{id}' contains '{text[markup]}', which the text renderer parses as a rich-text tag.");
            }

            if (text.IndexOf('�') >= 0)
                report.Errors.Add($"'{id}' contains a replacement character — the text was mangled on the way in.");
        }

        /// <summary>
        /// The shop's voice may be warm, wry and may address the reader ("you"), but it is never a person:
        /// first person plus jokes at the real author's expense was the recognizable signature of the copied
        /// source. Runs over masked text because 26 real book titles contain a first-person word
        /// ("Shatter Me", "The Fault in Our Stars").
        /// </summary>
        private static void ValidateFirstPerson(
            string id,
            string text,
            IEnumerable<string> maskedPhrases,
            BookDescriptionDraftReport report)
        {
            foreach (var token in Mask(text, maskedPhrases).Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var hit = string.Equals(token, "I", StringComparison.Ordinal)
                          || FirstPersonWords.Contains(token);
                if (!hit) continue;

                report.Errors.Add(
                    $"'{id}' speaks in the first person ('{token}'); the description presents the book, " +
                    "it is not the bookseller talking.");
                return;
            }
        }

        /// <summary>
        /// The point of the rewrite: the new text must not be the old one, nor a paraphrase of it.
        /// </summary>
        private static void ValidateAgainstSeed(
            BookDescriptionDraft draft,
            SeedEntry seedEntry,
            IEnumerable<string> maskedPhrases,
            BookDescriptionDraftReport report)
        {
            var old = seedEntry.Description;
            if (string.IsNullOrWhiteSpace(old)) return;

            if (string.Equals(Normalize(draft.New), Normalize(old), StringComparison.OrdinalIgnoreCase))
            {
                report.Errors.Add($"'{draft.Id}' repeats the description it is supposed to replace.");
                return;
            }

            var shared = LongestSharedRun(draft.New, old, maskedPhrases);
            if (shared >= OverlapWindowWords)
            {
                report.Warnings.Add(
                    $"'{draft.Id}' shares a {shared}-word run with the description it replaces (title and " +
                    "author excluded from the comparison) — check it is not a paraphrase.");
            }
        }

        /// <summary>
        /// Overlap with ANY seeded description, not just this book's own. A writer who was accidentally handed
        /// the old corpus produces text that echoes some other book's blurb, and comparing each draft only
        /// against its own predecessor would never see it. This is the check that actually enforces clean-room.
        /// </summary>
        private static void ValidateAgainstCorpus(
            BookDescriptionDraft draft,
            IReadOnlyDictionary<string, List<string>> corpus,
            IEnumerable<string> maskedPhrases,
            BookDescriptionDraftReport report)
        {
            var shingles = Shingles(Mask(draft.New, maskedPhrases));
            if (shingles.Count == 0) return;

            foreach (var shingle in shingles)
            {
                if (!corpus.TryGetValue(shingle, out var owners)) continue;

                foreach (var owner in owners)
                {
                    if (string.Equals(owner, draft.Id, StringComparison.OrdinalIgnoreCase))
                        continue; // its own predecessor is reported by ValidateAgainstSeed

                    report.Warnings.Add(
                        $"'{draft.Id}' shares a {OverlapWindowWords}-word run with the seeded description of " +
                        $"'{owner}' — the text may have been written from the old corpus.");
                    return;
                }
            }
        }

        private static void ValidateLength(string id, string text, BookDescriptionDraftReport report)
        {
            if (text.Length < MinReasonableLength)
            {
                report.Warnings.Add(
                    $"'{id}' is {text.Length} chars, shorter than the usual {MinReasonableLength}.");
            }
            else if (text.Length > MaxReasonableLength)
            {
                report.Warnings.Add(
                    $"'{id}' is {text.Length} chars, longer than the usual {MaxReasonableLength} — check the card layout.");
            }
        }

        /// <summary>
        /// A writer that does not know the book invents a plot. Two checks that do not rely on the writer's
        /// own report: a year in the text that the catalogue disagrees with, and the unrecognized flag itself.
        /// </summary>
        private static void ValidateFacts(
            BookDescriptionDraft draft,
            BookConfig book,
            BookDescriptionDraftReport report)
        {
            foreach (Match match in YearPattern.Matches(draft.New))
            {
                if (int.TryParse(match.Value, out var year) && year != book.Published)
                {
                    report.Warnings.Add(
                        $"'{draft.Id}' mentions the year {year} but the catalogue says it was published in " +
                        $"{book.Published} — verify the claim.");
                }
            }

            if (!draft.Recognized)
            {
                report.Warnings.Add(
                    $"'{draft.Id}' was written without knowing the book (recognized: false) — read it for " +
                    "invented plot, characters or places.");
                report.NeedsFactCheck.Add(draft.Id);
            }
        }

        /// <summary>
        /// An applied row must match what the game actually serves. Catches an apply that was never synced and
        /// a text edited after it went live — the one job live localization still has here.
        /// </summary>
        private static void ValidateApplied(
            BookDescriptionDraft draft,
            BookConfig book,
            IReadOnlyDictionary<string, string> localization,
            BookDescriptionDraftReport report)
        {
            if (!string.Equals(draft.Status, BookDescriptionDraft.StatusApplied, StringComparison.OrdinalIgnoreCase))
                return;

            var live = Lookup(localization, book.DescriptionKey);
            if (!string.Equals((live ?? string.Empty).Trim(), draft.New.Trim(), StringComparison.Ordinal))
            {
                report.Errors.Add(
                    $"'{draft.Id}' is marked applied but localization serves different text — the apply was " +
                    "lost, or the draft changed afterwards.");
            }
        }

        // ---------- text helpers ----------

        /// <summary>
        /// Longest run of consecutive words the two texts share, after removing the masked phrases
        /// (title, author) from both. Those are facts about the book: a description naming its own title
        /// reproduces a long run legitimately, and counting it would flag honest text.
        /// </summary>
        public static int LongestSharedRun(string left, string right, IEnumerable<string> maskedPhrases)
        {
            var leftWords = Words(Mask(left, maskedPhrases));
            var rightWords = Words(Mask(right, maskedPhrases));
            if (leftWords.Length == 0 || rightWords.Length == 0) return 0;

            var best = 0;
            var previousRow = new int[rightWords.Length + 1];
            var currentRow = new int[rightWords.Length + 1];

            for (var i = 1; i <= leftWords.Length; i++)
            {
                for (var j = 1; j <= rightWords.Length; j++)
                {
                    currentRow[j] = leftWords[i - 1] == rightWords[j - 1]
                        ? previousRow[j - 1] + 1
                        : 0;

                    if (currentRow[j] > best) best = currentRow[j];
                }

                var swap = previousRow;
                previousRow = currentRow;
                currentRow = swap;
                Array.Clear(currentRow, 0, currentRow.Length);
            }

            return best;
        }

        /// <summary>
        /// Word shingles of <see cref="OverlapWindowWords"/> length. A shared run of that many words is
        /// exactly a shared shingle, which turns the corpus check into a dictionary lookup instead of
        /// hundreds of pairwise comparisons.
        /// </summary>
        public static HashSet<string> Shingles(string text)
        {
            var words = Words(text);
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (words.Length < OverlapWindowWords) return result;

            for (var i = 0; i + OverlapWindowWords <= words.Length; i++)
                result.Add(string.Join(" ", words, i, OverlapWindowWords));

            return result;
        }

        private static Dictionary<string, List<string>> BuildCorpusIndex(IReadOnlyDictionary<string, SeedEntry> seed)
        {
            var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var pair in seed)
            {
                var entry = pair.Value;
                if (string.IsNullOrWhiteSpace(entry?.Description)) continue;

                var masked = Mask(entry.Description, new[] { entry.Title, entry.Author });
                foreach (var shingle in Shingles(masked))
                {
                    if (!index.TryGetValue(shingle, out var owners))
                    {
                        owners = new List<string>();
                        index[shingle] = owners;
                    }

                    owners.Add(pair.Key);
                }
            }

            return index;
        }

        private static string Mask(string text, IEnumerable<string> maskedPhrases)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (maskedPhrases == null) return text;

            foreach (var phrase in maskedPhrases)
            {
                if (string.IsNullOrWhiteSpace(phrase)) continue;
                text = text.Replace(phrase, " ", StringComparison.OrdinalIgnoreCase);

                // Titles carry subtitles after ';' and authors come as lists; mask the parts too, so
                // "The Hobbit" still masks when the catalogue title is "The Hobbit; The Lord of the Rings #0".
                foreach (var part in phrase.Split(';', '&', ','))
                {
                    var trimmed = part.Trim();
                    if (trimmed.Length >= 4)
                        text = text.Replace(trimmed, " ", StringComparison.OrdinalIgnoreCase);
                }
            }

            return text;
        }

        private static string[] Words(string text)
            => string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : text.ToLowerInvariant().Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

        private static string Normalize(string text)
            => string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : string.Join(" ", Words(text));

        private static bool AnchorMatches(string recorded, string current)
            => !string.IsNullOrWhiteSpace(recorded)
               && string.Equals(recorded.Trim(), (current ?? string.Empty).Trim(), StringComparison.Ordinal);

        private static string Lookup(IReadOnlyDictionary<string, string> localization, string key)
            => key != null && localization.TryGetValue(key, out var value) ? value : null;

        // ---------- loading ----------

        public sealed class SeedEntry
        {
            public string Title { get; set; }
            public string Author { get; set; }
            public string Description { get; set; }
        }

        private static Dictionary<string, SeedEntry> LoadSeed(string path, BookDescriptionDraftReport report)
        {
            if (!File.Exists(path))
            {
                report.Errors.Add($"Seed archive not found: {path}");
                return null;
            }

            try
            {
                var seed = JsonConvert.DeserializeObject<Dictionary<string, SeedEntry>>(File.ReadAllText(path));
                if (seed == null || seed.Count == 0)
                {
                    report.Errors.Add($"Seed archive is empty: {path}");
                    return null;
                }

                report.SeedRows = seed.Count;
                return new Dictionary<string, SeedEntry>(seed, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                return null;
            }
        }

        private static Dictionary<string, string> LoadLocalization(string path, BookDescriptionDraftReport report)
        {
            if (!File.Exists(path))
            {
                report.Errors.Add($"File not found: {path}");
                return null;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in root.Properties())
                    map[property.Name] = property.Value?.ToString();
                return map;
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                return null;
            }
        }

        private static bool TryLoad<T>(string path, BookDescriptionDraftReport report, out List<T> items)
        {
            items = new List<T>();

            if (!File.Exists(path))
            {
                report.Errors.Add($"File not found: {path}");
                return false;
            }

            try
            {
                items = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
                return true;
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                return false;
            }
        }
    }

    public sealed class BookDescriptionDraftReport
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public SortedSet<int> Batches { get; } = new();

        /// <summary>Ids whose writer did not know the book — the shortlist worth reading by hand.</summary>
        public List<string> NeedsFactCheck { get; } = new();

        public string DraftsPath { get; set; }
        public int CheckedDrafts { get; set; }
        public int SeedRows { get; set; }

        /// <summary>Drafts that passed every error-level check.</summary>
        public int Ready { get; set; }

        public bool HasErrors => Errors.Count > 0;

        public string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Checked {CheckedDrafts} draft(s) in {DraftsPath} against {SeedRows} seeded row(s); {Ready} ready.");
            sb.AppendLine($"Errors: {Errors.Count}   Warnings: {Warnings.Count}");
            if (Batches.Count > 0)
                sb.AppendLine($"Batches present: {string.Join(", ", Batches)}");
            if (NeedsFactCheck.Count > 0)
                sb.AppendLine($"Written without knowing the book: {string.Join(", ", NeedsFactCheck)}");
            return sb.ToString().TrimEnd();
        }
    }
}
