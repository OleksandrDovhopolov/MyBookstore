#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Game.WorldHud.SmokeTest
{
    // Temporary World HUD Phase 0 smoke-test. Spawns a placeholder cube in the scene,
    // attaches a bubble over it, then detaches and destroys the cube.
    // Remove once first real consumer (CustomerThoughtBubble) is verified to work end-to-end.
    public sealed class WorldHudSmokeRunner : IStartable, IDisposable
    {
        private readonly IWorldHudManager _worldHud;
        private readonly CancellationTokenSource _cts = new();

        public WorldHudSmokeRunner(IWorldHudManager worldHud) => _worldHud = worldHud;

        public void Start() => RunAsync(_cts.Token).Forget();

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                Debug.Log("[WorldHudSmoke] waiting 1s, then spawning cube");
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "WorldHudSmokeCube";
                cube.transform.position = Vector3.zero;
                cube.GetComponent<Collider>().enabled = false;

                Debug.Log("[WorldHudSmoke] attaching bubble");
                var bubble = await _worldHud.AttachAsync<SmokeWorldHudBubble>(
                    cube.transform,
                    new WorldHudArgs(offset: new Vector3(0f, 1.5f, 0f), billboard: true),
                    ct);

                if (bubble == null)
                {
                    Debug.LogError("[WorldHudSmoke] AttachAsync returned null. Check Addressables address WorldHud/SmokeWorldHudBubble.");
                    return;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: ct);

                Debug.Log("[WorldHudSmoke] detaching bubble");
                await _worldHud.DetachAsync(bubble, ct);

                UnityEngine.Object.Destroy(cube);
                Debug.Log("[WorldHudSmoke] finished successfully");
            }
            catch (OperationCanceledException) { /* shutdown — ok */ }
            catch (Exception e)
            {
                Debug.LogError($"[WorldHudSmoke] failed: {e}");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
#endif
