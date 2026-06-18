using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.WorldHud
{
    public interface IWorldHudManager
    {
        UniTask<T> AttachAsync<T>(Transform target, WorldHudArgs args = null, CancellationToken ct = default)
            where T : WorldHud;

        UniTask DetachAsync(WorldHud hud, CancellationToken ct = default);

        // Returns the bubble currently attached to the target, or null if none.
        // Only one bubble per target is allowed at a time.
        T GetAttached<T>(Transform target) where T : WorldHud;
    }
}
