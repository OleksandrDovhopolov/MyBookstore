using System;
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

        // Hides every active HUD (without detaching) until the returned handle is disposed. Ref-counted:
        // HUDs reappear only when the last outstanding handle is released, so overlapping suppressors
        // (e.g. several focused windows) never show HUDs back prematurely.
        IDisposable SuppressAll();
    }
}
