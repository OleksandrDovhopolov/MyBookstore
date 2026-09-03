using UnityEditor;
using UnityEngine;

namespace Book.Sell.Editor
{
    /// <summary>
    /// Editor menu wrapper around <see cref="ActiveRequestValidator"/>. The same check runs automatically as
    /// a build gate (<c>PreBuildValidationGate</c>); this is the on-demand version for content authoring.
    /// </summary>
    public static class ActiveRequestValidatorMenu
    {
        private const string MenuPath = "Tools/Configs/Validate Active Requests";
        private const string LogPrefix = "[RequestValidator]";

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            var report = ActiveRequestValidator.Validate();
            var summary = report.BuildSummary();

            foreach (var error in report.Errors)
                Debug.LogError($"{LogPrefix} {error}");
            foreach (var warning in report.Warnings)
                Debug.LogWarning($"{LogPrefix} {warning}");

            if (report.HasErrors)
                Debug.LogWarning($"{LogPrefix} {summary}");
            else
                Debug.Log($"{LogPrefix} {summary}");

            EditorUtility.DisplayDialog(
                report.HasErrors ? $"Active Requests — {report.Errors.Count} problem(s)" : "Active Requests OK",
                summary + (report.HasErrors ? "\n\nDetails are in the Console." : string.Empty),
                "OK");
        }
    }
}
