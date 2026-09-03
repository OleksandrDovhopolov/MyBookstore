using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Localization
{
    public sealed class LocalizationService : ILocalizationService
    {
        private const string DefaultLocale = "en";
        private const string LogPrefix = "[Localization]";

        private static readonly string[] Domains =
        {
            "ui",
            "dialogues",
            "quests",
            "characters",
            "items"
        };

        private readonly IConfigSource _configSource;
        private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);
        private readonly HashSet<string> _missingWarnings = new(StringComparer.Ordinal);

        private bool _warmed;

        public LocalizationService(IConfigSource configSource)
        {
            _configSource = configSource ?? throw new ArgumentNullException(nameof(configSource));
            CurrentLocale = DefaultLocale;
        }

        public string CurrentLocale { get; private set; }
        public event Action<string> LocaleChanged;

        public UniTask WarmupAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            LoadLocale(CurrentLocale);
            _warmed = true;
            Debug.Log($"{LogPrefix} Loaded {_texts.Count} string(s) for locale '{CurrentLocale}'.");
            return UniTask.CompletedTask;
        }

        public string Get(string key)
        {
            if (TryGet(key, out var value))
                return value;

            if (!string.IsNullOrWhiteSpace(key) && _missingWarnings.Add(key))
                Debug.LogWarning($"{LogPrefix} Missing key '{key}' for locale '{CurrentLocale}'.");

            return key ?? string.Empty;
        }

        public string Get(string key, params object[] args)
        {
            var value = Get(key);
            if (args == null || args.Length == 0)
                return value;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, value, args);
            }
            catch (FormatException ex)
            {
                Debug.LogWarning($"{LogPrefix} Bad format for key '{key}': {ex.Message}");
                return value;
            }
        }

        public bool TryGet(string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = string.Empty;
                return false;
            }

            return _texts.TryGetValue(key, out value);
        }

        public void SetLocale(string locale)
        {
            locale = NormalizeLocale(locale);
            if (string.Equals(CurrentLocale, locale, StringComparison.Ordinal))
                return;

            CurrentLocale = locale;
            _missingWarnings.Clear();
            if (_warmed)
                LoadLocale(CurrentLocale);
            LocaleChanged?.Invoke(CurrentLocale);
        }

        private void LoadLocale(string locale)
        {
            _texts.Clear();

            for (var i = 0; i < Domains.Length; i++)
            {
                var fileName = $"localization_{Domains[i]}_{locale}";
                var raw = _configSource.GetRaw(fileName);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    Debug.LogWarning($"{LogPrefix} Missing localization file '{fileName}.json'.");
                    continue;
                }

                MergeFile(fileName, raw);
            }
        }

        private void MergeFile(string fileName, string raw)
        {
            JObject table;
            try
            {
                table = JObject.Parse(raw, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to parse '{fileName}.json': {ex.Message}");
                return;
            }

            foreach (var property in table.Properties())
            {
                var key = property.Name;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var value = property.Value.Type switch
                {
                    JTokenType.Null => string.Empty,
                    JTokenType.String => property.Value.Value<string>(),
                    _ => property.Value.ToString(Formatting.None)
                };

                if (_texts.ContainsKey(key))
                {
                    Debug.LogError($"{LogPrefix} Duplicate key '{key}' in '{fileName}.json'. Keeping the first value.");
                    continue;
                }

                _texts[key] = value ?? string.Empty;
            }
        }

        private static string NormalizeLocale(string locale)
            => string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale.Trim().ToLowerInvariant();
    }
}
