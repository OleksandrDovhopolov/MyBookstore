using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.WorldHud
{
    public sealed class WorldHudManager : IWorldHudManager
    {
        private readonly IWorldHudFactory _factory;
        private readonly Dictionary<Transform, WorldHud> _attached = new();

        public WorldHudManager(IWorldHudFactory factory)
        {
            _factory = factory;
        }

        public async UniTask<T> AttachAsync<T>(
            Transform target,
            WorldHudArgs args = null,
            CancellationToken ct = default)
            where T : WorldHud
        {
            if (target == null) return null;
            args ??= WorldHudArgs.Default;

            // Enforce one-bubble-per-target: detach any existing bubble first.
            if (_attached.TryGetValue(target, out var existing) && existing != null)
            {
                await DetachAsync(existing, ct);
            }

            var hud = await _factory.CreateAsync<T>(ct);
            if (hud == null) return null;

            var cam = Camera.main; // Phase 0 — main camera is enough; revisit if multi-camera setup arrives.
            hud.AttachInternal(target, args, cam);
            hud.transform.SetParent(target, worldPositionStays: false);
            hud.gameObject.SetActive(true);

            _attached[target] = hud;
            return hud;
        }

        public async UniTask DetachAsync(WorldHud hud, CancellationToken ct = default)
        {
            if (hud == null) return;

            // Remove from the registry first so a concurrent AttachAsync to the same target doesn't try
            // to re-detach the same hud.
            if (hud.Target != null) _attached.Remove(hud.Target);

            await hud.DetachAsync(ct);
            _factory.Destroy(hud);
        }

        public T GetAttached<T>(Transform target) where T : WorldHud
        {
            if (target == null) return null;
            return _attached.TryGetValue(target, out var hud) ? hud as T : null;
        }
    }
}
