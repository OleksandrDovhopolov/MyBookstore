using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Localization
{
    public interface ILocalizationService
    {
        string CurrentLocale { get; }
        event Action<string> LocaleChanged;

        UniTask WarmupAsync(CancellationToken ct);
        string Get(string key);
        string Get(string key, params object[] args);
        bool TryGet(string key, out string value);
        void SetLocale(string locale);
    }
}
