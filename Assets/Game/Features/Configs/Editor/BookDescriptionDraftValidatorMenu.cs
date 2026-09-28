using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// The two content-authoring entry points for the book-description rewrite:
    ///
    /// <list type="bullet">
    /// <item><description><b>Validate</b> — errors and warnings only, for iterating on a batch.</description></item>
    /// <item><description><b>Report</b> — regenerates <see cref="BookDescriptionRewriteReport.OutputPath"/>:
    /// the change journal, with each rewrite next to the text it replaces and next to the metadata it has to
    /// agree with. Whether a description actually signals its genre is a judgement call, so the report puts
    /// the facts in front of the reviewer instead of pretending to automate it.</description></item>
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
            string markdown;
            try
            {
                markdown = BookDescriptionRewriteReport.Build();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogPrefix} Could not build the report: {ex.Message}");
                return;
            }

            var path = BookDescriptionRewriteReport.OutputPath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, markdown);

            Debug.Log($"{LogPrefix} Wrote {path} ({markdown.Length} chars). Read it for the per-book changes.");
            EditorUtility.DisplayDialog("Book Descriptions", $"Report written to\n{path}", "OK");
        }
    }
}
