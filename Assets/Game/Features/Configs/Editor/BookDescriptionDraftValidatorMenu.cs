using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// The two content-authoring entry points for the book-description rewrite:
    ///
    /// <list type="bullet">
    /// <item><description><b>Validate</b> — errors and warnings only, for iterating on a batch.</description></item>
    /// <item><description><b>Report</b> — regenerates the change journal: a one-page summary plus one file per
    /// batch, each rewrite next to the text it replaces and next to the metadata it has to agree with.
    /// Whether a description actually signals its genre is a judgement call, so the report puts the facts in
    /// front of the reviewer instead of pretending to automate it.</description></item>
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
            IReadOnlyDictionary<string, string> files;
            try
            {
                files = BookDescriptionRewriteReport.BuildAll();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Could not build the report: {ex.Message}");
                return;
            }

            foreach (var file in files)
            {
                var directory = Path.GetDirectoryName(file.Key);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(file.Key, file.Value);
            }

            // A batch file for a batch that no longer exists would keep reporting books that moved or were
            // dropped. Removing it here keeps the committed journal equal to a fresh run, which is what the
            // parity test checks.
            var removed = DeleteStaleBatchFiles(files.Keys);

            Debug.Log($"{LogPrefix} Wrote {files.Count} report file(s): {string.Join(", ", files.Keys)}"
                      + (removed.Count > 0 ? $"; removed stale {string.Join(", ", removed)}" : string.Empty));

            EditorUtility.DisplayDialog(
                "Book Descriptions",
                $"Report written:\n{string.Join("\n", files.Keys)}",
                "OK");
        }

        private static List<string> DeleteStaleBatchFiles(IEnumerable<string> written)
        {
            var removed = new List<string>();
            if (!Directory.Exists(BookDescriptionRewriteReport.BatchDirectory)) return removed;

            var keep = new HashSet<string>(written, StringComparer.OrdinalIgnoreCase);

            foreach (var path in Directory.GetFiles(
                         BookDescriptionRewriteReport.BatchDirectory,
                         BookDescriptionRewriteReport.BatchFilePrefix + "*.md"))
            {
                if (keep.Contains(path.Replace('\\', '/'))) continue;

                File.Delete(path);
                removed.Add(Path.GetFileName(path));
            }

            return removed;
        }
    }
}
