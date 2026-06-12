using System.Threading;
using Cysharp.Threading.Tasks;

namespace Infrastructure
{
    /// <summary>
    /// Инициализация Addressables и обновление каталогов с CDN (Cloudflare R2).
    /// Вызывается один раз на бутстрапе ДО прогрева конфигов и загрузки префабов через wrapper.
    /// </summary>
    public interface IAddressablesCatalogService
    {
        /// <summary>True после успешного InitializeAsync.</summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Инициализирует Addressables и подтягивает обновлённые каталоги, если они есть на CDN.
        /// Безопасно вызывать повторно — повторный вызов no-op.
        /// </summary>
        UniTask InitializeAndUpdateAsync(CancellationToken ct);
    }
}
