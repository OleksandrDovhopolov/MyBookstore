using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.LocationUnlock.API;
using Save;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// "Unlock" and "Lock" buttons per location config. Both go through the force seams of
    /// <see cref="ILocationUnlockService"/>, so unlock conditions (soldTotal / soldGenre) and item
    /// cost are bypassed — the player-facing TryUnlockAsync would refuse for Market and Village until
    /// their counters are ground out.
    /// Locking sticks only for locations that have an unlockCost: the ones without it (loc_park) are
    /// re-opened by the service's own auto-unlock pass on the next sale or launch, so the button logs
    /// a warning instead of pretending the change is permanent.
    /// </summary>
    public sealed class LocationUnlockCheatModule : ICheatsModule
    {
        private const string Group = "Locations";
        private const string LogTag = "[LocationUnlockCheat]";

        private readonly ILocationUnlockService _locationUnlock;
        private readonly IConfigsService _configs;
        private readonly ISaveService _save;
        private readonly CancellationToken _ct;

        public LocationUnlockCheatModule(
            ILocationUnlockService locationUnlock,
            IConfigsService configs,
            ISaveService save,
            CancellationToken ct)
        {
            _locationUnlock = locationUnlock ?? throw new ArgumentNullException(nameof(locationUnlock));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            var configs = _configs.GetAll<LocationConfig>();
            if (configs == null || configs.Count == 0)
            {
                Debug.LogWarning($"{LogTag} no LocationConfig entries - no buttons added.");
                return;
            }

            for (var i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;

                var id = config.Id;
                var displayName = string.IsNullOrEmpty(config.DisplayName) ? id : config.DisplayName;
                var hasCost = config.UnlockCost != null && config.UnlockCost.Length > 0;

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Unlock {displayName}", () => UnlockAsync(id).Forget())
                        .WithGroup(Group));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Lock {displayName}", () => LockAsync(id, hasCost).Forget())
                        .WithGroup(Group));
            }
        }

        private async UniTaskVoid UnlockAsync(string locationId)
        {
            try
            {
                var unlocked = await _locationUnlock.ForceUnlockAsync(locationId, _ct);
                if (!unlocked)
                {
                    Debug.Log($"{LogTag} '{locationId}' is already unlocked or missing.");
                    return;
                }

                await _save.SaveAsync(_ct);
                Debug.Log($"{LogTag} unlocked '{locationId}'.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid LockAsync(string locationId, bool hasUnlockCost)
        {
            try
            {
                var locked = await _locationUnlock.ForceLockAsync(locationId, _ct);
                if (!locked)
                {
                    Debug.Log($"{LogTag} '{locationId}' is already locked or missing.");
                    return;
                }

                await _save.SaveAsync(_ct);
                Debug.Log($"{LogTag} locked '{locationId}'. Spent unlock cost is not refunded.");

                if (!hasUnlockCost)
                {
                    Debug.LogWarning($"{LogTag} '{locationId}' has no unlockCost, so the service " +
                                     $"auto-unlocks it again on the next sale or launch.");
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
