using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Configs.Remote
{
    /// <summary>
    /// Абстракция над Firebase Remote Config — отвязывает Configs от Firebase SDK.
    /// Реальная реализация (FirebaseRemoteConfigService) живёт в Game.Bootstrap
    /// за define BOOKSTORE_FIREBASE_RC; здесь только контракт + Null-заглушка.
    /// </summary>
    public interface IRemoteConfigService
    {
        UniTask InitializeAsync(CancellationToken ct);
        bool TryGetString(string key, out string value);
    }

    /// <summary>Заглушка: RC отсутствует/выключен → нет override'ов (поведение = base configs).</summary>
    public sealed class NullRemoteConfigService : IRemoteConfigService
    {
        public UniTask InitializeAsync(CancellationToken ct) => UniTask.CompletedTask;

        public bool TryGetString(string key, out string value)
        {
            value = null;
            return false;
        }
    }
}
