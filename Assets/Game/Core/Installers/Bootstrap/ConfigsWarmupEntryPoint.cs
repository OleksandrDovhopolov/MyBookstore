using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Remote;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Прогревает конфиги один раз на старте приложения: сначала активирует Firebase RC
    /// (override-слой), затем загружает базовые конфиги. После этого Get&lt;T&gt; работает
    /// синхронно из кэша, а RC-override'ы применяются при ленивой десериализации.
    /// </summary>
    public sealed class ConfigsWarmupEntryPoint : IAsyncStartable
    {
        private readonly IConfigsService _configs;
        private readonly IRemoteConfigService _remoteConfig;

        public ConfigsWarmupEntryPoint(IConfigsService configs, IRemoteConfigService remoteConfig)
        {
            _configs = configs;
            _remoteConfig = remoteConfig;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            try
            {
                // RC должен быть активирован до первого Get (override применяется при десериализации).
                if (_remoteConfig != null)
                {
                    try
                    {
                        await _remoteConfig.InitializeAsync(cancellation);
                    }
                    catch (System.Exception ex) when (ex is not System.OperationCanceledException)
                    {
                        Debug.LogWarning($"[ConfigsWarmupEntryPoint] RC init failed, continuing with base configs: {ex.Message}");
                    }
                }

                await _configs.WarmupAsync(cancellation);
                Debug.Log("[ConfigsWarmupEntryPoint] Configs warmed up.");
            }
            catch (System.OperationCanceledException)
            {
                // shutdown during async — ignore
            }
        }
    }
}
