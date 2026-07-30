using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Book.Sell.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Build.Editor
{
    /// <summary>
    /// Fails the player build before it starts when content that only breaks at runtime is wrong.
    /// <para>
    /// docs/BUILD.md is a manual checklist, and the two items it marks as mandatory are exactly the ones a
    /// human forgets: re-syncing configs into StreamingAssets (the build reads them from there, never from
    /// <c>Assets/Configs</c>) and keeping active requests solvable. Both fail silently — the APK starts fine
    /// and the content is simply wrong. A build callback cannot be forgotten, so the checklist becomes the
    /// backstop instead of the primary guard.
    /// </para>
    /// <para>
    /// Runs on every player build. Throwing <see cref="BuildFailedException"/> aborts it.
    /// </para>
    /// </summary>
    public sealed class PreBuildValidationGate : IPreprocessBuildWithReport
    {
        private const string LogPrefix = "[PreBuild]";
        private const string MenuPath = "Tools/Configs/Run Pre-Build Validation";

        private const string ConfigsDir = "Assets/Configs";
        private const string BundledDir = "Assets/StreamingAssets/Configs";
        private const string ManifestFileName = "manifest.json";

        // Ahead of Addressables (1000) and most third-party callbacks, so a content error surfaces before
        // the slow parts of the build have run.
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = Collect();
            if (errors.Count == 0)
            {
                Debug.Log($"{LogPrefix} content validation passed.");
                return;
            }

            var message = new StringBuilder()
                .AppendLine($"{LogPrefix} {errors.Count} content problem(s) — build aborted.")
                .AppendLine("Fix these, or see docs/BUILD.md:");
            foreach (var error in errors)
                message.AppendLine($"  - {error}");

            var text = message.ToString();
            Debug.LogError(text);
            throw new BuildFailedException(text);
        }

        /// <summary>Same checks, run on demand — useful before starting a long build.</summary>
        [MenuItem(MenuPath)]
        public static void RunManually()
        {
            var errors = Collect();
            var summary = errors.Count == 0
                ? "All pre-build content checks passed."
                : $"{errors.Count} problem(s) would abort a player build:\n\n" +
                  string.Join("\n", errors.Select(e => "- " + e));

            if (errors.Count == 0) Debug.Log($"{LogPrefix} {summary}");
            else Debug.LogError($"{LogPrefix} {summary}");

            EditorUtility.DisplayDialog(
                errors.Count == 0 ? "Pre-Build Validation OK" : $"Pre-Build Validation — {errors.Count} problem(s)",
                summary,
                "OK");
        }

        private static List<string> Collect()
        {
            var errors = new List<string>();
            CollectBundledConfigErrors(errors);
            CollectActiveRequestErrors(errors);
            return errors;
        }

        /// <summary>
        /// The player reads configs from StreamingAssets via manifest.json, so any drift from
        /// <c>Assets/Configs</c> ships stale content. Mirrors what
        /// <c>Tools/Configs/Sync Bundled Defaults to StreamingAssets</c> would produce.
        /// </summary>
        private static void CollectBundledConfigErrors(List<string> errors)
        {
            if (!Directory.Exists(ConfigsDir))
            {
                errors.Add($"Source config directory is missing: {ConfigsDir}");
                return;
            }

            if (!Directory.Exists(BundledDir))
            {
                errors.Add($"{BundledDir} does not exist — run Tools/Configs/Sync Bundled Defaults to StreamingAssets.");
                return;
            }

            var source = FileNames(ConfigsDir);
            var bundled = FileNames(BundledDir).Where(n => n != ManifestFileName).ToList();

            var missing = source.Except(bundled, StringComparer.OrdinalIgnoreCase).ToList();
            var extra = bundled.Except(source, StringComparer.OrdinalIgnoreCase).ToList();

            if (missing.Count > 0)
                errors.Add($"Not bundled into StreamingAssets: {string.Join(", ", missing)} — run the Sync menu.");

            if (extra.Count > 0)
                errors.Add($"Stale files in StreamingAssets that no longer exist in {ConfigsDir}: " +
                           $"{string.Join(", ", extra)} — run the Sync menu.");

            foreach (var name in source.Intersect(bundled, StringComparer.OrdinalIgnoreCase))
            {
                var a = File.ReadAllText(Path.Combine(ConfigsDir, name));
                var b = File.ReadAllText(Path.Combine(BundledDir, name));
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    errors.Add($"'{name}' differs between {ConfigsDir} and StreamingAssets — run the Sync menu.");
            }

            var manifestPath = Path.Combine(BundledDir, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                errors.Add($"{ManifestFileName} is missing in StreamingAssets — the build cannot enumerate configs.");
                return;
            }

            // StreamingAssetsConfigSource can only see files listed here; Directory.GetFiles is unavailable
            // on the player side, so an unlisted file is invisible even when shipped.
            var listed = ParseManifest(manifestPath, errors);
            if (listed == null) return;

            var unlisted = bundled.Except(listed, StringComparer.OrdinalIgnoreCase).ToList();
            if (unlisted.Count > 0)
                errors.Add($"Bundled but absent from {ManifestFileName}: {string.Join(", ", unlisted)} — " +
                           "they will not load in the build. Run the Sync menu.");
        }

        private static void CollectActiveRequestErrors(List<string> errors)
        {
            var report = ActiveRequestValidator.Validate();
            if (!report.HasErrors) return;

            foreach (var error in report.Errors)
                errors.Add($"Active requests: {error}");
        }

        private static List<string> FileNames(string dir)
            => Directory.GetFiles(dir, "*.json")
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static List<string> ParseManifest(string path, List<string> errors)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path))
                       ?? new List<string>();
            }
            catch (Exception ex)
            {
                errors.Add($"{ManifestFileName} is not a valid JSON string array: {ex.Message}");
                return null;
            }
        }
    }
}
