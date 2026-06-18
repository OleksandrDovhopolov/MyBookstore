using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Editor menu: копирует Assets/Configs/*.json в Assets/StreamingAssets/Configs/
    /// и регенерирует manifest.json. Запускать перед каждым релизным билдом.
    /// </summary>
    public static class SyncBundledDefaultsMenu
    {
        private const string MenuPath = "Tools/Configs/Sync Bundled Defaults to StreamingAssets";
        private const string LogPrefix = "[BundledDefaults]";

        private const string SourceDir = "Assets/Configs";
        private const string TargetDir = "Assets/StreamingAssets/Configs";
        private const string ManifestFileName = "manifest.json";

        [MenuItem(MenuPath)]
        public static void Sync()
        {
            if (!Directory.Exists(SourceDir))
            {
                Debug.LogError($"{LogPrefix} Source directory not found: {SourceDir}");
                return;
            }

            Directory.CreateDirectory(TargetDir);

            // Wipe existing copies + manifest (and their .meta) — full overwrite.
            foreach (var path in Directory.GetFiles(TargetDir, "*.json"))
                File.Delete(path);
            foreach (var path in Directory.GetFiles(TargetDir, "*.json.meta"))
                File.Delete(path);

            var sources = Directory.GetFiles(SourceDir, "*.json")
                .Select(p => Path.GetFileName(p))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var name in sources)
            {
                var from = Path.Combine(SourceDir, name);
                var to = Path.Combine(TargetDir, name);
                File.Copy(from, to, overwrite: true);
            }

            var manifestPath = Path.Combine(TargetDir, ManifestFileName);
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(sources));

            AssetDatabase.Refresh();

            Debug.Log($"{LogPrefix} Synced {sources.Length} file(s) to {TargetDir} + manifest.");
        }
    }
}
