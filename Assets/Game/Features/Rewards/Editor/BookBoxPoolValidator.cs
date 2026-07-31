using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Configs;
using Game.Configs.Models;
using Game.Rewards.Services;
using Newtonsoft.Json;

namespace Game.Rewards.Editor
{
    /// <summary>
    /// Checks that every book-box shop lot can actually deliver books. Pure — logs nothing, shows nothing;
    /// callers decide how to surface the report (console + dialog for the menu, aborted build for the gate).
    /// <para>
    /// The failure this exists for is silent and expensive: <see cref="BookBoxRewardExpander"/> rolls from a
    /// pool built by a hardcoded predicate over <see cref="BookConfig"/>, and a book catalog that simply does
    /// not carry the field a predicate reads leaves every book on the C# default. That is what happened when
    /// the catalog was swapped to one without <c>rarityWeight</c>: all books fell back to
    /// <c>RarityWeight = 0.5</c>, the <c>book_box_rare_8</c> rule (<c>&gt;= 0.6</c>) matched nothing, and the
    /// lot took the player's gold and returned an empty spec. Nothing about that is a parse error, so no
    /// existing check caught it.
    /// </para>
    /// <para>
    /// Replays the real <see cref="BookBoxPoolRules"/> predicates rather than restating them, so the verdict
    /// cannot drift from gameplay — same approach <c>ActiveRequestValidator</c> takes with
    /// <c>BookConditionRequestEvaluator</c>. Config paths come from each type's <see cref="ConfigFileAttribute"/>,
    /// so renaming a config file cannot leave the validator pointing at a stale one.
    /// </para>
    /// </summary>
    public static class BookBoxPoolValidator
    {
        public const string ConfigsDir = "Assets/Configs";

        public static BookBoxPoolValidationReport Validate(string configsDir = ConfigsDir)
        {
            var report = new BookBoxPoolValidationReport();

            var booksPath = ConfigPath<BookConfig>(configsDir, report);
            var shopPath = ConfigPath<ShopConfig>(configsDir, report);
            if (booksPath == null || shopPath == null) return report;

            if (!TryLoad<BookConfig>(booksPath, report, out var books)) return report;
            if (!TryLoad<ShopConfig>(shopPath, report, out var lots)) return report;

            report.BookCount = books.Count;
            report.BooksPath = booksPath;

            ValidateLotsReferenceKnownBoxes(lots, report);
            ValidateEveryRuleCanFill(books, lots, report);

            return report;
        }

        /// <summary>
        /// A lot whose <c>rewardId</c> looks like a book box but has no rule only warns at runtime
        /// ("Unknown book-box id") and hands the player an empty spec.
        /// </summary>
        private static void ValidateLotsReferenceKnownBoxes(
            IReadOnlyList<ShopConfig> lots,
            BookBoxPoolValidationReport report)
        {
            foreach (var lot in lots)
            {
                var rewardId = lot?.RewardId;
                if (!BookBoxPoolRules.IsBookBoxId(rewardId)) continue;

                report.CheckedLots++;
                if (!BookBoxPoolRules.TryGet(rewardId, out _))
                    report.Errors.Add(
                        $"Shop lot '{lot.Id}' has rewardId '{rewardId}', which no BookBoxPoolRules rule " +
                        $"defines — the lot would take the price and deliver nothing. Add the rule in " +
                        $"{nameof(BookBoxPoolRules)} or fix the lot's rewardId.");
            }
        }

        /// <summary>
        /// The core check: on a fresh save nothing is owned, so the catalog match count is the ceiling of what
        /// a box can ever deliver. Zero matches means the box is permanently broken; fewer matches than
        /// <c>Rolls</c> means it under-delivers from the very first purchase.
        /// </summary>
        private static void ValidateEveryRuleCanFill(
            IReadOnlyList<BookConfig> books,
            IReadOnlyList<ShopConfig> lots,
            BookBoxPoolValidationReport report)
        {
            var soldBoxIds = new HashSet<string>(
                lots.Where(l => BookBoxPoolRules.IsBookBoxId(l?.RewardId)).Select(l => l.RewardId),
                StringComparer.Ordinal);

            foreach (var pair in BookBoxPoolRules.All)
            {
                var boxId = pair.Key;
                var rule = pair.Value;

                var matches = books.Count(b => b != null && !string.IsNullOrEmpty(b.Id) && rule.Filter(b));
                report.MatchesByBox[boxId] = matches;
                report.CheckedRules++;

                // A rule no lot sells is authoring-in-progress: the player cannot reach it, so it cannot
                // take their gold. Recorded for visibility, never failed — otherwise every half-authored
                // box blocks the build. It gets enforced the moment a lot starts selling it.
                if (!soldBoxIds.Contains(boxId))
                {
                    report.UnsoldBoxes.Add(boxId);
                    continue;
                }

                if (matches == 0)
                {
                    report.Errors.Add(
                        $"Book box '{boxId}' is sold in shop.json but matches 0 of {books.Count} books " +
                        $"(rule: {rule.FilterDescription}) — the lot takes the price and delivers nothing. " +
                        $"Most often the catalog lacks the field the rule reads, so every book sits on its " +
                        $"C# default.");
                    continue;
                }

                if (matches < rule.Rolls)
                    report.Errors.Add(
                        $"Book box '{boxId}' rolls {rule.Rolls} book(s) but only {matches} book(s) in the " +
                        $"catalog match its rule ({rule.FilterDescription}) — even a fresh save under-delivers.");
            }
        }

        private static string ConfigPath<T>(string configsDir, BookBoxPoolValidationReport report)
        {
            var attribute = typeof(T).GetCustomAttribute<ConfigFileAttribute>();
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.FileName))
            {
                report.Errors.Add($"{typeof(T).Name} has no [ConfigFile] attribute — cannot locate its file.");
                return null;
            }

            return Path.Combine(configsDir, attribute.FileName + ".json").Replace('\\', '/');
        }

        private static bool TryLoad<T>(string path, BookBoxPoolValidationReport report, out List<T> items)
        {
            items = null;

            if (!File.Exists(path))
            {
                report.Errors.Add($"File not found: {path}");
                return false;
            }

            try
            {
                items = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                return false;
            }

            if (items.Count != 0) return true;

            report.Errors.Add($"{path} contains no entries.");
            return false;
        }
    }

    public sealed class BookBoxPoolValidationReport
    {
        public List<string> Errors { get; } = new();

        /// <summary>Books in the catalog matching each box rule, by box id.</summary>
        public Dictionary<string, int> MatchesByBox { get; } = new(StringComparer.Ordinal);

        /// <summary>Rules with no shop lot selling them — reported for awareness, not as errors.</summary>
        public List<string> UnsoldBoxes { get; } = new();

        public string BooksPath { get; set; }
        public int BookCount { get; set; }
        public int CheckedRules { get; set; }
        public int CheckedLots { get; set; }

        public bool HasErrors => Errors.Count > 0;

        public string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Checked {CheckedRules} book-box rule(s) and {CheckedLots} book-box shop lot(s) " +
                          $"against {BookCount} book(s) from {BooksPath}.");
            sb.AppendLine($"Problems: {Errors.Count}");
            if (UnsoldBoxes.Count > 0)
                sb.AppendLine($"Rules not sold by any lot: {string.Join(", ", UnsoldBoxes)}");

            sb.Append("Catalog books matching each box: ");
            sb.Append(MatchesByBox.Count == 0
                ? "none"
                : string.Join("  ", MatchesByBox.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => $"{p.Key}={p.Value}")));
            return sb.ToString();
        }

        public string FormatErrors()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Errors.Count; i++)
                sb.AppendLine($"  - {Errors[i]}");
            return sb.ToString();
        }
    }
}
