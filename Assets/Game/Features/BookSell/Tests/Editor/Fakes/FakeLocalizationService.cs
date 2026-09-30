using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Localization;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>Minimal ILocalizationService fake: a key returns whatever was put in the table, or nothing.</summary>
    public sealed class FakeLocalizationService : ILocalizationService
    {
        private readonly Dictionary<string, string> _table = new(StringComparer.Ordinal);

        public string CurrentLocale => "en";

        public event Action<string> LocaleChanged;

        public FakeLocalizationService Set(string key, string value)
        {
            _table[key] = value;
            return this;
        }

        public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

        public string Get(string key) => _table.TryGetValue(key, out var value) ? value : key;

        public string Get(string key, params object[] args)
            => _table.TryGetValue(key, out var value) ? string.Format(value, args) : key;

        public bool TryGet(string key, out string value) => _table.TryGetValue(key, out value);

        public void SetLocale(string locale) => LocaleChanged?.Invoke(locale);
    }
}
