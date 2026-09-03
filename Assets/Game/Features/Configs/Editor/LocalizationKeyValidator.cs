using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Verifies player-facing localization keys authored in JSON configs.
    /// </summary>
    public static class LocalizationKeyValidator
    {
        public const string ConfigsDir = "Assets/Configs";

        private static readonly string[] ConfigFiles =
        {
            "bookshops.json",
            "books.json",
            "characters.json",
            "consumables.json",
            "days.json",
            "decors.json",
            "dialogues.json",
            "hard_requests.json",
            "locations.json",
            "quest_items.json",
            "quests.json",
            "sample_requests.json",
            "shelf_presets.json",
            "shop.json"
        };

        private static readonly string[] LocalizationFiles =
        {
            "localization_ui_en.json",
            "localization_dialogues_en.json",
            "localization_quests_en.json",
            "localization_characters_en.json",
            "localization_items_en.json",
            "localization_books_en.json"
        };

        private static readonly HashSet<string> LocalizedFieldNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "displayNameKey",
            "roleKey",
            "descriptionKey",
            "titleKey",
            "authorKey",
            "speakerKey",
            "textKey"
        };

        public static LocalizationKeyValidationReport Validate(string configsDir = ConfigsDir)
        {
            var report = new LocalizationKeyValidationReport();
            var available = LoadLocalizationKeys(configsDir, report);
            var required = CollectRequiredKeys(configsDir, report);

            foreach (var key in required.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (available.Contains(key)) continue;
                report.Errors.Add($"Missing localization key '{key}' referenced by {required[key]}.");
            }

            return report;
        }

        private static HashSet<string> LoadLocalizationKeys(string configsDir, LocalizationKeyValidationReport report)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fileName in LocalizationFiles)
            {
                var path = Path.Combine(configsDir, fileName);
                if (!File.Exists(path))
                {
                    report.Errors.Add($"Localization file missing: {path}");
                    continue;
                }

                JObject table;
                try
                {
                    table = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                    continue;
                }

                foreach (var property in table.Properties())
                {
                    if (string.IsNullOrWhiteSpace(property.Name)) continue;
                    if (!keys.Add(property.Name))
                        report.Errors.Add($"Duplicate localization key '{property.Name}' across localization files.");
                }
            }

            return keys;
        }

        private static Dictionary<string, string> CollectRequiredKeys(
            string configsDir,
            LocalizationKeyValidationReport report)
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var fileName in ConfigFiles)
            {
                var path = Path.Combine(configsDir, fileName);
                if (!File.Exists(path))
                {
                    report.Errors.Add($"Config file missing: {path}");
                    continue;
                }

                JToken root;
                try
                {
                    root = JToken.Parse(File.ReadAllText(path));
                }
                catch (JsonException ex)
                {
                    report.Errors.Add($"Failed to parse {path}: {ex.Message}");
                    continue;
                }

                CollectKeys(root, fileName, "$", keys);
            }

            return keys;
        }

        private static void CollectKeys(
            JToken token,
            string fileName,
            string path,
            Dictionary<string, string> keys)
        {
            switch (token)
            {
                case JObject obj:
                    foreach (var property in obj.Properties())
                    {
                        var childPath = $"{path}.{property.Name}";
                        if (LocalizedFieldNames.Contains(property.Name))
                        {
                            var value = property.Value.Type == JTokenType.String
                                ? property.Value.Value<string>()
                                : null;
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                if (!keys.ContainsKey(value))
                                    keys[value] = $"{fileName}:{childPath}";
                            }
                        }

                        CollectKeys(property.Value, fileName, childPath, keys);
                    }
                    break;

                case JArray array:
                    for (var i = 0; i < array.Count; i++)
                        CollectKeys(array[i], fileName, $"{path}[{i}]", keys);
                    break;
            }
        }
    }

    public sealed class LocalizationKeyValidationReport
    {
        public List<string> Errors { get; } = new();
        public bool HasErrors => Errors.Count > 0;
    }
}
