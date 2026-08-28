using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Privacy.Editor
{
    /// <summary>
    /// REL-5 release blocker: refuses to build if the first-run consent screen has no public privacy
    /// policy URL to link to. A consent screen whose link goes nowhere is worse than no screen at all.
    /// </summary>
    public sealed class PrivacyLinksBuildCheck : IPreprocessBuildWithReport
    {
        private const string PrivacyUrlField = "_privacyPolicyUrl";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:BootstrapInstaller");
            if (guids.Length == 0)
            {
                // Nothing to validate — the installer asset is what carries the URL.
                Debug.LogWarning("[Consent] No BootstrapInstaller asset found; skipping privacy URL validation.");
                return;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;

                var url = new SerializedObject(asset).FindProperty(PrivacyUrlField)?.stringValue;
                if (IsValid(url)) continue;

                throw new BuildFailedException(
                    $"[Consent] REL-5 release blocker: '{PrivacyUrlField}' on {path} is " +
                    $"'{url ?? "<missing>"}'. The first-run consent screen must link to a live https " +
                    "privacy policy before this build can ship.");
            }
        }

        private static bool IsValid(string url) =>
            !string.IsNullOrWhiteSpace(url) &&
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
