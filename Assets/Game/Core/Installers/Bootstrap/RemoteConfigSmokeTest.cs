// Smoke-test RC override-слоя. В отличие от ConfigsSmokeTest (Phase 1, без RC),
// реально инициализирует Firebase, фетчит Remote Config и применяет override.
// Требует define BOOKSTORE_FIREBASE_RC + импортированный FirebaseRemoteConfig SDK.
// Повесить на GameObject в сцене → Play → смотреть Console.
#if BOOKSTORE_FIREBASE_RC
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Configs.Remote;
using UnityEngine;

namespace Game.Bootstrap
{
    [Obsolete("Test Class")]
    public class RemoteConfigSmokeTest : MonoBehaviour
    {
        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                // 1. Firebase dependencies (CheckAndFixDependencies).
                var loader = new RemoteConfigLoader();
                await loader.EnsureDependenciesAsync(ct);
                Debug.Log("[RemoteConfigSmokeTest] 1/4 Firebase deps ready.");

                // 2. Fetch + activate RC (логи статуса/ключей внутри сервиса).
                var rc = new FirebaseRemoteConfigService();
                await rc.InitializeAsync(ct);
                Debug.Log("[RemoteConfigSmokeTest] 2/4 RC initialized.");

                // 3. ConfigsService с РЕАЛЬНЫМ RC override (не Null!).
                var service = new ConfigsService(new LocalFolderConfigSource(), new RemoteConfigOverrideSource(rc));
                await service.WarmupAsync(ct);
                Debug.Log("[RemoteConfigSmokeTest] 3/4 Configs warmed up.");

                // 4. Read — ждём 999, если RC применился.
                var dune = service.Get<BookConfig>("book_dune");
                Debug.Log(
                    $"[RemoteConfigSmokeTest] 4/4 book_dune price={dune?.BasePrice} " +
                    $"(ожидаем 999 при сработавшем RC, 120 — если override не применился)");
            }
            catch (OperationCanceledException)
            {
                // shutdown during async — ignore
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteConfigSmokeTest] ERROR: {ex}");
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
#endif
