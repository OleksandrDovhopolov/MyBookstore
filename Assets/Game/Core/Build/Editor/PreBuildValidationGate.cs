using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Book.Sell.Editor;
using Game.Configs.Editor;
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
    /// docs/BUILD.md is a manual checklist, and the items it marks as mandatory are exactly the ones a
    /// human forgets: keeping bundled defaults synced, keeping active requests solvable, and checking for
    /// dead config files. These fail silently — the APK starts fine and the content is simply wrong or stale.
    /// A build callback cannot be forgotten, so the checklist becomes the backstop instead of the primary guard.
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
            var validation = Collect();
            LogWarnings(validation.Warnings);

            if (validation.Errors.Count == 0)
            {
                var suffix = validation.Warnings.Count > 0 ? $" with {validation.Warnings.Count} warning(s)" : string.Empty;
                Debug.Log($"{LogPrefix} content validation passed{suffix}.");
                return;
            }

            var message = new StringBuilder()
                .AppendLine($"{LogPrefix} {validation.Errors.Count} content problem(s) — build aborted.")
                .AppendLine("Fix these, or see docs/BUILD.md:");
            foreach (var error in validation.Errors)
                message.AppendLine($"  - {error}");

            var text = message.ToString();
            Debug.LogError(text);
            throw new BuildFailedException(text);
        }

        /// <summary>Same checks, run on demand — useful before starting a long build.</summary>
        [MenuItem(MenuPath)]
        public static void RunManually()
        {
            var report = Collect();
            var summary = BuildManualSummary(report);

            LogWarnings(report.Warnings);

            if (report.Errors.Count == 0) Debug.Log($"{LogPrefix} {summary}");
            else Debug.LogError($"{LogPrefix} {summary}");

            EditorUtility.DisplayDialog(
                DialogTitle(report),
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
            ("Character memories", CollectCharacterMemoryReferenceErrors),
            ("Localization keys", CollectLocalizationKeyErrors),
            ("Book box pools", CollectBookBoxPoolErrors),
        };

        /// <summary>
        /// Warning-level checks: useful hygiene signals that should be fixed, but do not make the shipped
        /// player content wrong by themselves.
        /// </summary>
        private static readonly (string Name, Action<List<string>> Run)[] SoftValidators =
        {
            ("Orphan configs", CollectOrphanConfigWarnings),
        };

        private static ValidationReport Collect()
        {
            var report = new ValidationReport();
            RunValidators(Validators, report.Errors, report.Errors);
            RunValidators(SoftValidators, report.Warnings, report.Errors);
            return report;
        }

        private static void RunValidators(
            (string Name, Action<List<string>> Run)[] validators,
            List<string> findings,
            List<string> errors)
        {
            foreach (var validator in validators)
            {
                var before = findings.Count;
                try
                {
                    validator.Run(findings);
                }
                catch (Exception ex)
                {
                    // A validator that throws must not mask the checks after it, and must not let a build
                    // through on the strength of "no errors collected".
                    errors.Add($"{validator.Name}: validator itself failed ({ex.GetType().Name}): {ex.Message}");
                    continue;
                }

                // Prefix here rather than in each collector, so every check reads the same way in the log.
                for (var i = before; i < findings.Count; i++)
                    findings[i] = $"{validator.Name}: {findings[i]}";
            }
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

        private static void CollectCharacterMemoryReferenceErrors(List<string> errors)
            => errors.AddRange(CharacterMemoryReferenceValidator.Validate().Errors);

        private static void CollectLocalizationKeyErrors(List<string> errors)
            => errors.AddRange(LocalizationKeyValidator.Validate().Errors);

        /// <summary>
        /// A book-box shop lot whose pool matches no book takes the player's gold and returns nothing. The
        /// pool predicates read <c>BookConfig</c> fields directly, so a catalog missing a field leaves every
        /// book on its C# default and silently empties a box — no parse error, no missing reference.
        /// </summary>
        private static void CollectBookBoxPoolErrors(List<string> errors)
            => errors.AddRange(BookBoxPoolValidator.Validate().Errors);

        private static void CollectOrphanConfigWarnings(List<string> warnings)
        {
            if (!Directory.Exists(ConfigsDir)) return;

            var knownSections = new HashSet<string>(ConfigSectionCatalog.SectionNames, StringComparer.OrdinalIgnoreCase);
            foreach (var fileName in FileNames(ConfigsDir))
            {
                var section = Path.GetFileNameWithoutExtension(fileName);
                if (knownSections.Contains(section)) continue;
                if (section.StartsWith("localization_", StringComparison.OrdinalIgnoreCase)) continue;

                warnings.Add(
                    $"{fileName} ships into the APK as dead weight; no [ConfigFile] maps to section '{section}'.");
            }
        }

        private static void LogWarnings(IReadOnlyList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0) return;

            var message = new StringBuilder()
                .AppendLine($"{LogPrefix} {warnings.Count} warning(s):");
            foreach (var warning in warnings)
                message.AppendLine($"  - {warning}");
            Debug.LogWarning(message.ToString());
        }

        private static string BuildManualSummary(ValidationReport report)
        {
            var sb = new StringBuilder();
            if (report.Errors.Count == 0)
                sb.AppendLine("All pre-build content checks passed.");
            else
            {
                sb.AppendLine($"{report.Errors.Count} problem(s) would abort a player build:");
                sb.AppendLine();
                foreach (var error in report.Errors)
                    sb.AppendLine("- " + error);
            }

            if (report.Warnings.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine($"{report.Warnings.Count} warning(s):");
                sb.AppendLine();
                foreach (var warning in report.Warnings)
                    sb.AppendLine("- " + warning);
            }

            return sb.ToString().TrimEnd();
        }

        private static string DialogTitle(ValidationReport report)
        {
            if (report.Errors.Count > 0)
                return $"Pre-Build Validation — {report.Errors.Count} problem(s), {report.Warnings.Count} warning(s)";
            if (report.Warnings.Count > 0)
                return $"Pre-Build Validation OK — {report.Warnings.Count} warning(s)";
            return "Pre-Build Validation OK";
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

        private sealed class ValidationReport
        {
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();
        }
    }
}
