using UnityEditor;
using UnityEngine;

namespace Game.Rewards.Editor
{
    /// <summary>
    /// Editor menu wrapper around <see cref="BookBoxPoolValidator"/>. The same check runs automatically as a
    /// build gate (<c>PreBuildValidationGate</c>); this is the on-demand version for content authoring —
    /// notably right after swapping the books catalog, which is what breaks box pools.
    /// </summary>
    public static class BookBoxPoolValidatorMenu
    {
        private const string MenuPath = "Tools/Configs/Validate Book Box Pools";
        private const string LogPrefix = "[BookBoxValidator]";

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            var report = BookBoxPoolValidator.Validate();
            var summary = report.BuildSummary();

            foreach (var error in report.Errors)
                Debug.LogError($"{LogPrefix} {error}");

            if (report.HasErrors) Debug.LogWarning($"{LogPrefix} {summary}");
            else Debug.Log($"{LogPrefix} {summary}");

            EditorUtility.DisplayDialog(
                report.HasErrors ? $"Book Box Pools — {report.Errors.Count} problem(s)" : "Book Box Pools OK",
                summary + (report.HasErrors ? "\n\nDetails are in the Console." : string.Empty),
                "OK");
        }
    }
}
