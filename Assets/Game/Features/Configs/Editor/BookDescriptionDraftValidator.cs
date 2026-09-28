using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Checks a batch of rewritten book descriptions (book_descriptions_v2.json) before it replaces the
    /// live text. The live catalogue text came from an external sheet and is being rewritten from scratch
    /// (CONTENT-1 in docs/RELEASE_TASKS.md), so the checks here are of two kinds:
    ///
    /// <list type="bullet">
    /// <item><description><b>Errors</b> — the draft is wrong as data: unknown or duplicate book, an anchor
    /// that no longer matches the catalogue, markup the text renderer would eat.</description></item>
    /// <item><description><b>Warnings</b> — the draft may be wrong as writing: unusual length, wording that
    /// still overlaps the text being replaced, vocabulary from the other setting. These need a human, not a
    /// gate — the n-gram check in particular cannot tell a legitimately repeated book title from copying.</description></item>
    /// </list>
    ///
    /// <para>Reads the json files directly: there is no <c>IConfigsService</c> outside Play mode.</para>
    /// </summary>
    public static class BookDescriptionDraftValidator
    {
        public const string ConfigsDir = "Assets/Configs";
        public const string DraftsFileName = "book_descriptions_v2.json";
        public const string BooksFileName = "books.json";
        public const string LocalizationFileName = "localization_books_en.json";

        /// <summary>p5..p95 of the seeded corpus (74..308, median 205) — editorial, not a UI limit.</summary>
        public const int MinReasonableLength = 130;
        public const int MaxReasonableLength = 265;

        /// <summary>
        /// Shared runs shorter than this are ordinary English ("a collection of short stories about").
        /// Title and author are masked out before counting, so a description naming its own book is fine.
        /// </summary>
        public const int OverlapWindowWords = 8;

        /// <summary>
        /// Proper nouns of the setting the seed text was written for. They are the clearest sign a draft
        /// leaned on the old wording; extend as more surface.
        /// </summary>
        private static readonly string[] ForeignSettingTerms = { "bookston", "bookstonbury", "bookstonian" };

        /// <summary>TMP parses these as rich-text tags and would swallow part of the sentence.</summary>
        private static readonly char[] MarkupChars = { '{', '}', '<', '>' };

        public static BookDescriptionDraftReport Validate(string configsDir = ConfigsDir)
        {
            var report = new BookDescriptionDraftReport();
            var draftsPath = Path.Combine(configsDir, DraftsFileName).Replace('\\', '/');
            report.DraftsPath = draftsPath;

            if (!TryLoad<BookDescriptionDraftConfig>(draftsPath, report, out var drafts)) return report;
            if (!TryLoad<BookConfig>(Path.Combine(configsDir, BooksFileName).Replace('\\', '/'), report, out var books))
                return report;

            var localization = LoadLocalization(
                Path.Combine(configsDir, LocalizationFileName).Replace('\\', '/'), report);
            if (localization == null) return report;

            var booksById = new Dictionary<string, BookConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var book in books)
            {
                if (book?.Id != null)
                    booksById[book.Id] = book;
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var draft in drafts)
            {
                if (draft == null)
                {
                    report.Errors.Add("Drafts file contains a null entry.");
                    continue;
                }

                report.CheckedDrafts++;
                ValidateDraft(draft, booksById, localization, seenIds, report);
            }

            return report;
        }

        private static void ValidateDraft(
            BookDescriptionDraftConfig draft,
            IReadOnlyDictionary<string, BookConfig> booksById,
            IReadOnlyDictionary<string, string> localization,
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

            // Excluded books are not this game's content; rewriting them would be wasted work that also
            // never shows up in the game (see BookConfig.IsExcludedFromCatalog).
            if (book.IsExcludedFromCatalog)
                report.Errors.Add($"'{id}' is excluded from the catalogue and must not be rewritten.");

            if (!BookDescriptionDraftConfig.IsAllowedStatus(draft.Status))
            {
                report.Errors.Add(
                    $"'{id}' has status '{draft.Status}'; expected one of " +
                    $"{string.Join(", ", BookDescriptionDraftConfig.AllowedStatuses)}.");
            }

            if (draft.Batch <= 0)
                report.Errors.Add($"'{id}' has no positive 'batch'.");
            else
                report.Batches.Add(draft.Batch);

            ValidateAnchors(draft, book, localization, report);

            var text = draft.New;
            if (string.IsNullOrWhiteSpace(text))
            {
                report.Errors.Add($"'{id}' has an empty 'new' description.");
                return;
            }

            ValidateFormatting(id, text, report);
            ValidateAgainstOldText(draft, book, localization, report);
            ValidateLength(id, text, report);
            ValidateForeignSettingTerms(id, text, report);

            if (report.Errors.Count == errorsBefore)
                report.Ready++;
        }

        /// <summary>
        /// Book ids are positional in the seed sheet, so a shifted row silently moves a draft onto another
        /// book. Comparing the title and author recorded at draft time against the catalogue catches it.
        /// </summary>
        private static void ValidateAnchors(
            BookDescriptionDraftConfig draft,
            BookConfig book,
            IReadOnlyDictionary<string, string> localization,
            BookDescriptionDraftReport report)
        {
            var title = Lookup(localization, book.TitleKey);
            var author = Lookup(localization, book.AuthorKey);

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
        /// The point of the rewrite: the new text must not be a paraphrase of the old one. `Old` is optional
        /// (it is filled in after writing, precisely so it cannot be an input), and when it is absent the
        /// live localization value serves as the comparison.
        /// </summary>
        private static void ValidateAgainstOldText(
            BookDescriptionDraftConfig draft,
            BookConfig book,
            IReadOnlyDictionary<string, string> localization,
            BookDescriptionDraftReport report)
        {
            var old = string.IsNullOrWhiteSpace(draft.Old)
                ? Lookup(localization, book.DescriptionKey)
                : draft.Old;
            if (string.IsNullOrWhiteSpace(old)) return;

            if (string.Equals(Normalize(draft.New), Normalize(old), StringComparison.OrdinalIgnoreCase))
            {
                report.Errors.Add($"'{draft.Id}' repeats the description it is supposed to replace.");
                return;
            }

            var masked = new[]
            {
                Lookup(localization, book.TitleKey),
                Lookup(localization, book.AuthorKey)
            };

            var shared = LongestSharedRun(draft.New, old, masked);
            if (shared >= OverlapWindowWords)
            {
                report.Warnings.Add(
                    $"'{draft.Id}' shares a {shared}-word run with the old description (title and author " +
                    $"excluded from the comparison) — check it is not a paraphrase.");
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

        private static void ValidateForeignSettingTerms(string id, string text, BookDescriptionDraftReport report)
        {
            foreach (var term in ForeignSettingTerms)
            {
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    report.Warnings.Add(
                        $"'{id}' mentions '{term}', which belongs to the setting the seed text was written for.");
                    return;
                }
            }
        }

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
                : text.ToLowerInvariant()
                    .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '(', ')', '-', '\'' },
                        StringSplitOptions.RemoveEmptyEntries);

        private static string Normalize(string text)
            => string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : string.Join(" ", Words(text));

        private static bool AnchorMatches(string recorded, string current)
            => !string.IsNullOrWhiteSpace(recorded)
               && string.Equals(recorded.Trim(), (current ?? string.Empty).Trim(), StringComparison.Ordinal);

        private static string Lookup(IReadOnlyDictionary<string, string> localization, string key)
            => key != null && localization.TryGetValue(key, out var value) ? value : null;

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

        public string DraftsPath { get; set; }
        public int CheckedDrafts { get; set; }

        /// <summary>Drafts that passed every error-level check.</summary>
        public int Ready { get; set; }

        public bool HasErrors => Errors.Count > 0;

        public string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Checked {CheckedDrafts} draft(s) in {DraftsPath}; {Ready} ready.");
            sb.AppendLine($"Errors: {Errors.Count}   Warnings: {Warnings.Count}");
            if (Batches.Count > 0)
                sb.AppendLine($"Batches present: {string.Join(", ", Batches)}");
            return sb.ToString().TrimEnd();
        }
    }
}
