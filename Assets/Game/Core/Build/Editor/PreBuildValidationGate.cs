using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Book.Sell.Editor;
using Game.Rewards.Editor;
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

        /// <summary>
        /// The registry of everything the gate enforces. Adding a check means adding one row here and
        /// nothing else — the runner, the message prefix, the menu and the failure path are shared.
        /// <para>
        /// Keep this list in sync with the table in <c>docs/BUILD.md §0</c>; that table is the human-facing
        /// copy and drifts if a check is added here only.
        /// </para>
        /// <para>
        /// A check belongs here when it (a) reads content that only breaks at runtime, and (b) can run
        /// outside Play mode — i.e. off raw JSON, without <c>IConfigsService</c>. Checks needing a live
        /// container (e.g. <c>ItemReferenceValidator</c>) cannot join until they are refactored to load
        /// files directly; see the "Known gap" note in docs/BUILD.md.
        /// </para>
        /// </summary>
        private static readonly (string Name, Action<List<string>> Run)[] Validators =
        {
            ("Bundled configs", CollectBundledConfigErrors),
            ("Active requests", CollectActiveRequestErrors),
            ("Dialogue delivered conditions", CollectDialogueDeliveredReferenceErrors),
            ("Book box pools", CollectBookBoxPoolErrors),
        };

        private static List<string> Collect()
        {
            var errors = new List<string>();
            foreach (var validator in Validators)
            {
                var before = errors.Count;
                try
                {
                    validator.Run(errors);
                }
                catch (Exception ex)
                {
                    // A validator that throws must not mask the checks after it, and must not let a build
                    // through on the strength of "no errors collected".
                    errors.Add($"{validator.Name}: validator itself failed ({ex.GetType().Name}): {ex.Message}");
                    continue;
                }

                // Prefix here rather than in each collector, so every check reads the same way in the log.
                for (var i = before; i < errors.Count; i++)
                    errors[i] = $"{validator.Name}: {errors[i]}";
            }

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
            => errors.AddRange(ActiveRequestValidator.Validate().Errors);

        private static void CollectDialogueDeliveredReferenceErrors(List<string> errors)
            => errors.AddRange(DialogueDeliveredConditionReferenceValidator.Validate().Errors);

        /// <summary>
        /// A book-box shop lot whose pool matches no book takes the player's gold and returns nothing. The
        /// pool predicates read <c>BookConfig</c> fields directly, so a catalog missing a field leaves every
        /// book on its C# default and silently empties a box — no parse error, no missing reference.
        /// </summary>
        private static void CollectBookBoxPoolErrors(List<string> errors)
            => errors.AddRange(BookBoxPoolValidator.Validate().Errors);

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
