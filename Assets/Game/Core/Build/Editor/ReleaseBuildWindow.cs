using System;
using System.IO;
using System.Linq;
using System.Text;
using Game.Configs.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Build.Editor
{
    public sealed class ReleaseBuildWindow : EditorWindow
    {
        private enum BuildVariant
        {
            Dev = 0,
            Prod = 1
        }

        private enum AndroidPackageFormat
        {
            APK = 0,
            AAB = 1
        }

        private const string MenuPath = "Tools/Build/Release Build Window";
        private const string DefaultOutputFolder = "Builds";
        private const string KeystorePasswordEnvVar = "ANDROID_KEYSTORE_PASSWORD";
        private const int RecommendedMinSdk = 24;
        private const int RecommendedTargetSdk = 35;

        private static readonly string[] VariantLabels = { "Dev", "Prod" };
        private static readonly string[] FormatLabels = { "APK", "AAB" };

        private BuildVariant _variant = BuildVariant.Dev;
        private AndroidPackageFormat _format = AndroidPackageFormat.APK;
        private string _outputFolder = DefaultOutputFolder;
        private string _keystorePassword = string.Empty;
        private bool _autoIncrementVersionCode = true;
        private Vector2 _scroll;
        private bool _isBusy;
        private string _status;
        private MessageType _statusType = MessageType.None;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<ReleaseBuildWindow>(utility: false, title: "Release Build");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _keystorePassword = Environment.GetEnvironmentVariable(KeystorePasswordEnvVar) ?? string.Empty;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            using (new EditorGUI.DisabledScope(_isBusy))
            {
                DrawBuildOptions();
                EditorGUILayout.Space();
                DrawKeystoreSection();
                EditorGUILayout.Space();
                DrawCurrentSettings();
                EditorGUILayout.Space();
            }

            DrawBuildButton();
            DrawStatus();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBuildOptions()
        {
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            _variant = (BuildVariant)GUILayout.Toolbar((int)_variant, VariantLabels);
            _format = (AndroidPackageFormat)GUILayout.Toolbar((int)_format, FormatLabels);
            _outputFolder = EditorGUILayout.TextField("Output Folder", string.IsNullOrWhiteSpace(_outputFolder)
                ? DefaultOutputFolder
                : _outputFolder);

            if (_variant == BuildVariant.Prod && _format == AndroidPackageFormat.AAB)
            {
                _autoIncrementVersionCode = EditorGUILayout.Toggle("Auto-increment versionCode", _autoIncrementVersionCode);
                EditorGUILayout.HelpBox(
                    "Google Play requires a higher versionCode for every uploaded AAB. Enabled only for Prod + AAB so local Dev/APK builds do not burn version numbers.",
                    MessageType.None);
            }
        }

        /// <summary>
        /// versionCode is only bumped for Prod AAB builds — the artifacts that actually get uploaded to
        /// Google Play, which rejects a reused code. Dev/APK builds are local throwaways and keep the number.
        /// </summary>
        private bool ShouldAutoIncrementVersionCode()
            => _variant == BuildVariant.Prod && _format == AndroidPackageFormat.AAB && _autoIncrementVersionCode;

        private void DrawKeystoreSection()
        {
            if (_variant != BuildVariant.Prod)
            {
                EditorGUILayout.HelpBox("Dev builds use Unity debug signing. Prod builds use configured Android keystore.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Prod Signing", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Custom Keystore", PlayerSettings.Android.useCustomKeystore);
                EditorGUILayout.TextField("Keystore", EmptyAsPlaceholder(PlayerSettings.Android.keystoreName));
                EditorGUILayout.TextField("Alias", EmptyAsPlaceholder(PlayerSettings.Android.keyaliasName));
            }

            _keystorePassword = EditorGUILayout.PasswordField("Password", _keystorePassword);

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(KeystorePasswordEnvVar)))
            {
                EditorGUILayout.HelpBox(
                    $"Password is kept only in this editor session. You can also provide {KeystorePasswordEnvVar}.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox($"{KeystorePasswordEnvVar} is set and was loaded for this session.", MessageType.Info);
            }
        }

        private static string EmptyAsPlaceholder(string value)
            => string.IsNullOrWhiteSpace(value) ? "<not set>" : value;

        private void DrawCurrentSettings()
        {
            EditorGUILayout.LabelField("Current Settings", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Active Target", EditorUserBuildSettings.activeBuildTarget.ToString());
                EditorGUILayout.Toggle("Development", EditorUserBuildSettings.development);
                EditorGUILayout.Toggle("Build App Bundle", EditorUserBuildSettings.buildAppBundle);
                EditorGUILayout.TextField("Scripting Backend", PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString());
                EditorGUILayout.TextField("Architectures", PlayerSettings.Android.targetArchitectures.ToString());
                EditorGUILayout.TextField("Min SDK", PlayerSettings.Android.minSdkVersion.ToString());
                EditorGUILayout.TextField("Target SDK", PlayerSettings.Android.targetSdkVersion.ToString());
                EditorGUILayout.TextField("Version Name", PlayerSettings.bundleVersion);
                EditorGUILayout.IntField("Version Code", PlayerSettings.Android.bundleVersionCode);
                EditorGUILayout.IntField("Enabled Scenes", GetEnabledScenes().Length);
            }

            var warnings = BuildSettingsWarnings();
            if (!string.IsNullOrEmpty(warnings))
            {
                EditorGUILayout.HelpBox(warnings, MessageType.Warning);
            }
        }

        private void DrawBuildButton()
        {
            var label = _isBusy ? "Building..." : $"Build {_variant} {_format}";
            using (new EditorGUI.DisabledScope(_isBusy))
            {
                if (GUILayout.Button(label, GUILayout.Height(30f)))
                {
                    RunBuildWithDialog();
                }
            }
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, _statusType);
            }
        }

        private void RunBuildWithDialog()
        {
            var outputPath = BuildOutputPath();
            var errors = ValidateInputs(outputPath);
            if (errors.Length > 0)
            {
                SetStatus(string.Join(Environment.NewLine, errors), MessageType.Error);
                return;
            }

            var summary = $"Variant: {_variant}\n" +
                          $"Format: {_format}\n" +
                          $"Output: {outputPath}\n" +
                          $"Signing: {(_variant == BuildVariant.Prod ? PlayerSettings.Android.keyaliasName : "Unity debug keystore")}";

            if (ShouldAutoIncrementVersionCode())
            {
                var current = PlayerSettings.Android.bundleVersionCode;
                summary += $"\nVersion Code: {current} -> {current + 1}";
            }

            if (!EditorUtility.DisplayDialog("Start Android Build?", summary, "Build", "Cancel"))
            {
                return;
            }

            RunBuild(outputPath);
        }

        private string[] ValidateInputs(string outputPath)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (GetEnabledScenes().Length == 0)
            {
                errors.Add("No enabled scenes found in Build Settings.");
            }

            if (string.IsNullOrWhiteSpace(_outputFolder))
            {
                errors.Add("Output folder is empty.");
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                errors.Add("Output path is empty.");
            }

            if (_variant == BuildVariant.Prod)
            {
                if (!PlayerSettings.Android.useCustomKeystore)
                {
                    errors.Add("Prod build requires PlayerSettings.Android.useCustomKeystore.");
                }

                if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName))
                {
                    errors.Add("Prod build requires Android keystore path.");
                }

                if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                {
                    errors.Add("Prod build requires Android key alias.");
                }

                if (string.IsNullOrWhiteSpace(_keystorePassword))
                {
                    errors.Add($"Prod build requires keystore password field or {KeystorePasswordEnvVar}.");
                }
            }

            return errors.ToArray();
        }

        private void RunBuild(string outputPath)
        {
            _isBusy = true;
            try
            {
                SetStatus("Switching active build target to Android...", MessageType.Info);
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                    !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    throw new BuildFailedException("Failed to switch active build target to Android.");
                }

                SetStatus("Applying Android build settings...", MessageType.Info);
                ApplyBuildSettings();
                LogBuildSettingsWarnings();

                SetStatus("Syncing bundled configs...", MessageType.Info);
                SyncBundledDefaultsMenu.Sync();

                SetStatus("Running pre-build validation...", MessageType.Info);
                var validationErrors = PreBuildValidationGate.CollectErrors();
                if (validationErrors.Count > 0)
                {
                    throw new BuildFailedException(BuildValidationErrorMessage(validationErrors));
                }

                // Addressables content is deliberately NOT built here. AddressableAssetSettings has
                // "Build Addressables on Player Build" enabled, so BuildPlayer below rebuilds the bundles
                // through Addressables' own build callback. Calling BuildPlayerContent() here as well
                // produced two content builds per click. Leaving it to the callback also keeps a plain
                // Unity "Build" safe — it cannot ship stale bundles. PreBuildValidationGate still runs
                // first (callbackOrder 0, ahead of Addressables' 1000), so content errors surface early.
                SetStatus("Building Android player (Addressables content builds with it)...", MessageType.Info);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? DefaultOutputFolder);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = GetEnabledScenes(),
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = _variant == BuildVariant.Dev ? BuildOptions.Development : BuildOptions.None
                });

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException($"Player build finished with {report.summary.result}: {report.summary.totalErrors} error(s).");
                }

                SetStatus($"Build succeeded: {outputPath}\nSize: {EditorUtility.FormatBytes((long)report.summary.totalSize)}", MessageType.Info);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ReleaseBuild] " + ex);
                SetStatus(ex.Message, MessageType.Error);
            }
            finally
            {
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                _isBusy = false;
                Repaint();
            }
        }

        private void ApplyBuildSettings()
        {
            EditorUserBuildSettings.development = _variant == BuildVariant.Dev;
            EditorUserBuildSettings.buildAppBundle = _format == AndroidPackageFormat.AAB;

            if (_variant != BuildVariant.Prod)
            {
                return;
            }

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystorePass = _keystorePassword;
            PlayerSettings.Android.keyaliasPass = _keystorePassword;

            if (ShouldAutoIncrementVersionCode())
            {
                // Bump before the build so the produced AAB carries the new code. A failed build leaves a
                // gap in the sequence, which Google Play accepts — it only requires strictly increasing.
                PlayerSettings.Android.bundleVersionCode += 1;
            }
        }

        private static string[] GetEnabledScenes()
            => EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

        private string BuildOutputPath()
        {
            var folder = string.IsNullOrWhiteSpace(_outputFolder) ? DefaultOutputFolder : _outputFolder.Trim();
            var extension = _format == AndroidPackageFormat.AAB ? "aab" : "apk";
            var product = SanitizeFileName(PlayerSettings.productName);
            var variant = _variant.ToString().ToLowerInvariant();
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            return Path.Combine(folder, $"{product}-{variant}-{timestamp}.{extension}");
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string((string.IsNullOrWhiteSpace(value) ? "MyBookstore" : value)
                .Select(ch => invalid.Contains(ch) ? '-' : ch)
                .ToArray());
            return safe.Trim('-', ' ');
        }

        private static string BuildValidationErrorMessage(System.Collections.Generic.IReadOnlyList<string> errors)
        {
            var sb = new StringBuilder()
                .AppendLine($"{errors.Count} pre-build validation problem(s):");
            foreach (var error in errors)
            {
                sb.AppendLine("- " + error);
            }

            return sb.ToString().TrimEnd();
        }

        private static string BuildSettingsWarnings()
        {
            var warnings = new System.Collections.Generic.List<string>();

            if (PlayerSettings.Android.minSdkVersion < (AndroidSdkVersions)RecommendedMinSdk)
            {
                warnings.Add($"Min SDK is below recommended baseline: {PlayerSettings.Android.minSdkVersion} < {RecommendedMinSdk}.");
            }

            if (PlayerSettings.Android.targetSdkVersion != AndroidSdkVersions.AndroidApiLevelAuto &&
                PlayerSettings.Android.targetSdkVersion < (AndroidSdkVersions)RecommendedTargetSdk)
            {
                warnings.Add($"Target SDK is below recommended baseline: {PlayerSettings.Android.targetSdkVersion} < {RecommendedTargetSdk}.");
            }

            return warnings.Count == 0 ? string.Empty : string.Join(Environment.NewLine, warnings);
        }

        private static void LogBuildSettingsWarnings()
        {
            var warnings = BuildSettingsWarnings();
            if (!string.IsNullOrEmpty(warnings))
            {
                Debug.LogWarning("[ReleaseBuild] " + warnings);
            }
        }

        private void SetStatus(string status, MessageType type)
        {
            _status = status;
            _statusType = type;
            Repaint();
        }
    }
}
