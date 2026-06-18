using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI.SmokeTest
{
    // Temporary Phase 0 smoke-test. Remove after verification.
    //TODO after smoke test make this class internal again
    public sealed class SmokeRunner : IStartable, IDisposable
    {
        private readonly IUIManager _uiManager;
        private readonly CancellationTokenSource _cts = new();

        public SmokeRunner(IUIManager uiManager) => _uiManager = uiManager;

        public void Start() => RunAsync(_cts.Token).Forget();

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                Debug.Log("[Smoke] runner waiting 1s before first window");
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);

                await _uiManager.ShowAsync<SmokeMainPage>(ct: ct);
                await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);

                await _uiManager.ShowAsync<SmokeAdditionalPopup>(ct: ct);
                await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);

                await _uiManager.ShowAsync<SmokeSystemDialog>(new WindowArgs().AsSystem(), ct);
                await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);

                await _uiManager.HideAsync<SmokeSystemDialog>(ct: ct);
                await _uiManager.HideAsync<SmokeAdditionalPopup>(ct: ct);
                await _uiManager.HideAsync<SmokeMainPage>(ct: ct);

                Debug.Log("[Smoke] runner finished successfully");
            }
            catch (OperationCanceledException)
            {
                // graceful exit on scene/app shutdown
            }
            catch (Exception e)
            {
                Debug.LogError($"[Smoke] runner failed: {e}");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
