using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// On-demand check and review report for rewritten book descriptions
    /// (<see cref="BookDescriptionDraftValidator"/>). Two menu items:
    ///
    /// <list type="bullet">
    /// <item><description><b>Validate</b> — errors and warnings only, for iterating on a batch.</description></item>
    /// <item><description><b>Report</b> — prints each draft next to the metadata it must agree with
    /// (genre, qualities, year, pages). Whether a description actually signals its genre is a judgement
    /// call, so the report puts the facts in front of the reviewer rather than pretending to automate it.</description></item>
    /// </list>
    /// </summary>
    public static class BookDescriptionDraftValidatorMenu
    {
        private const string ValidateMenuPath = "Tools/Configs/Validate Book Descriptions";
        private const string ReportMenuPath = "Tools/Configs/Report Book Descriptions Batch";
        private const string LogPrefix = "[BookDescriptions]";

        [MenuItem(ValidateMenuPath)]
        public static void Validate()
        {
            var report = BookDescriptionDraftValidator.Validate();
            var summary = report.BuildSummary();

            foreach (var error in report.Errors)
                Debug.LogError($"{LogPrefix} {error}");

            foreach (var warning in report.Warnings)
                Debug.LogWarning($"{LogPrefix} {warning}");

            if (report.HasErrors) Debug.LogWarning($"{LogPrefix} {summary}");
            else Debug.Log($"{LogPrefix} {summary}");

            EditorUtility.DisplayDialog(
                report.HasErrors ? $"Book Descriptions — {report.Errors.Count} problem(s)" : "Book Descriptions OK",
                summary + "\n\nDetails are in the Console.",
                "OK");
        }

        [MenuItem(ReportMenuPath)]
        public static void Report()
        {
            var drafts = Load<BookDescriptionDraftConfig>(BooksExcelImporter.DraftsPath);
            if (drafts == null || drafts.Count == 0)
            {
                Debug.Log($"{LogPrefix} No drafts in {BooksExcelImporter.DraftsPath}.");
                return;
            }

            var books = Load<BookConfig>(BooksExcelImporter.DefaultOutputPath);
            if (books == null) return;

            var booksById = books.Where(book => book?.Id != null).ToDictionary(book => book.Id, book => book);
            var localization = LoadLocalization(BooksExcelImporter.DefaultLocalizationOutputPath);
            if (localization == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"{LogPrefix} {drafts.Count} draft(s):");

            foreach (var draft in drafts.Where(d => d != null).OrderBy(d => d.Batch).ThenBy(d => d.Id))
            {
                if (!booksById.TryGetValue(draft.Id ?? string.Empty, out var book))
                {
                    sb.AppendLine($"  {draft.Id}: not in the catalogue.");
                    continue;
                }

                var genres = book.Genres == null ? "-" : string.Join(", ", book.Genres);
                var qualities = book.Qualities == null ? "-" : string.Join(", ", book.Qualities);
                var old = localization.TryGetValue(book.DescriptionKey ?? string.Empty, out var live) ? live : "-";

                sb.AppendLine();
                sb.AppendLine($"  [batch {draft.Batch} / {draft.Status}] {draft.Id} — {draft.TitleAtDraft} · {draft.AuthorAtDraft}");
                sb.AppendLine($"    genres: {genres}");
                sb.AppendLine($"    qualities: {qualities}");
                sb.AppendLine($"    published: {book.Published}   pages: {book.Pages}");
                sb.AppendLine($"    old ({old.Length}): {old}");
                sb.AppendLine($"    new ({(draft.New ?? string.Empty).Length}): {draft.New}");
            }

            Debug.Log(sb.ToString());
        }

        private static List<T> Load<T>(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"{LogPrefix} File not found: {path}");
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to parse {path}: {ex.Message}");
                return null;
            }
        }

        private static Dictionary<string, string> LoadLocalization(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"{LogPrefix} File not found: {path}");
                return null;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                return root.Properties().ToDictionary(p => p.Name, p => p.Value?.ToString() ?? string.Empty);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to parse {path}: {ex.Message}");
                return null;
            }
        }
    }
}
