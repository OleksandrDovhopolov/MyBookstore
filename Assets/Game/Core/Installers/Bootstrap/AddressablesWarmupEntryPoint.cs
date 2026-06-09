using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Infrastructure;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Инициализирует Addressables и подтягивает обновлённый каталог с CDN до того, как
    /// фичи начнут грузить ассеты через ProdAddressablesWrapper. Запуск каталога не блокируется
    /// при сетевой ошибке — играем на встроенном каталоге, ошибка только логируется.
    /// </summary>
    public sealed class AddressablesWarmupEntryPoint : IAsyncStartable
    {
        private readonly IAddressablesCatalogService _catalog;

        public AddressablesWarmupEntryPoint(IAddressablesCatalogService catalog)
        {
            _catalog = catalog;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            try
            {
                await _catalog.InitializeAndUpdateAsync(cancellation);
                Debug.Log("[AddressablesWarmupEntryPoint] Addressables ready.");
            }
            catch (OperationCanceledException)
            {
                // shutdown — ignore
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesWarmupEntryPoint] Init failed, continuing with bundled catalog: {ex.Message}");
            }
        }
    }
}
