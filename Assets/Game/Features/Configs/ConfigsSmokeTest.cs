using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using UnityEngine;

namespace Game.Configs
{
    // Smoke-test системы конфигов (Phase 1, локальная папка Assets/Configs).
    // Повесить на любой GameObject в сцене и нажать Play. Без VContainer —
    // прямое ручное wiring, чтобы тест не зависел от настроек сцены/DI.
    //
    // Ожидаемый вывод в Console:
    //   loaded books=2, locations=2, requests=2, events=1
    //   book_dune -> Dune by Frank Herbert (price=120)
    //   IsExists(book_dune)=True, IsExists(book_missing)=False
    [Obsolete("Test Class")]
    public class ConfigsSmokeTest : MonoBehaviour
    {
        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // Source = локальная папка Assets/Configs (Application.dataPath/Configs в Editor).
            // Override = Null (RC выключен на Phase 1).
            var source = new LocalFolderConfigSource();
            var service = new ConfigsService(source, new NullConfigOverrideSource());

            try
            {
                await service.WarmupAsync(ct);

                Debug.Log(
                    $"[ConfigsSmokeTest] loaded " +
                    $"books={service.GetAll<BookConfig>().Count}, " +
                    $"locations={service.GetAll<LocationConfig>().Count}, " +
                    $"requests={service.GetAll<RequestConfig>().Count}, " +
                    $"events={service.GetAll<EventConfig>().Count}");

                var dune = service.Get<BookConfig>("book_dune");
                if (dune != null)
                    Debug.Log($"[ConfigsSmokeTest] book_dune -> {dune.Title} by {dune.Author} (price={dune.BasePrice})");
                else
                    Debug.LogError("[ConfigsSmokeTest] book_dune NOT found — проверь Assets/Configs/books.json");

                Debug.Log(
                    $"[ConfigsSmokeTest] IsExists(book_dune)={service.IsExists<BookConfig>("book_dune")}, " +
                    $"IsExists(book_missing)={service.IsExists<BookConfig>("book_missing")}");

                // TryGet pattern
                if (service.TryGet<LocationConfig>("loc_mall", out var mall))
                    Debug.Log($"[ConfigsSmokeTest] loc_mall -> {mall.DisplayName} (unlock={mall.UnlockCost})");

                Debug.Log("[ConfigsSmokeTest] Done.");
            }
            catch (OperationCanceledException)
            {
                // shutdown during async — ignore
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigsSmokeTest] ERROR: {ex}");
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
