using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Morning;
using Game.LocationUnlock.API;
using Game.Preparation.Services;
using UnityEngine;

namespace GameplayUI
{
    /// <summary>
    /// Day-1 "drop straight into the location" orchestrator (see docs/FTUE.md). Headless replay of what
    /// <c>PreparationWindow.ConfirmAsync</c> does, minus the UI: resolve the fixed day-1 location,
    /// auto-stock the shelf from the seeded inventory, then additively load the location scene.
    ///
    /// Not a DI service: constructed by <see cref="MainSceneBootstrap"/> from its injected dependencies,
    /// which keeps the installers assembly free of a reference to this UI assembly.
    ///
    /// Day-1 entry is free — unlike <c>PreparationWindow</c>, this flow does not charge the location entry fee.
    /// </summary>
    public sealed class FirstDayEntryFlow
    {
        private const string LogPrefix = "[FirstDayEntry]";

        private readonly IMorningSessionService _morning;
        private readonly IPreparationSessionService _preparation;
        private readonly IGameFlowService _gameFlow;
        private readonly IConfigsService _configs;
        private readonly ILocationUnlockService _locationUnlock;

        public FirstDayEntryFlow(
            IMorningSessionService morning,
            IPreparationSessionService preparation,
            IGameFlowService gameFlow,
            IConfigsService configs,
            ILocationUnlockService locationUnlock = null)
        {
            _morning = morning ?? throw new ArgumentNullException(nameof(morning));
            _preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            _gameFlow = gameFlow ?? throw new ArgumentNullException(nameof(gameFlow));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _locationUnlock = locationUnlock; // optional-safe: null → treat all locations as unlocked
        }

        /// <summary>
        /// Runs the direct entry. Returns true if the player was taken into the location; false if any
        /// precondition failed (no location, day not continuable, empty stock) so the caller can fall
        /// back to the hub.
        /// </summary>
        public async UniTask<bool> EnterAsync(CancellationToken ct)
        {
            var locationId = ResolveFixedLocationId();
            if (string.IsNullOrEmpty(locationId))
            {
                Debug.LogError($"{LogPrefix} no unlocked location found — falling back to hub.");
                return false;
            }

            // Establish the day context and move Morning → Preparation (mirrors GameplaySceneController.StartGameAsync).
            var continueResult = await _morning.ContinueToPreparationAsync(ct);
            if (continueResult == null)
            {
                Debug.LogWarning($"{LogPrefix} morning session cannot continue — falling back to hub.");
                return false;
            }

            // Auto-stock: seed the session for this location, fill the shelf from the seeded inventory, confirm.
            await _preparation.StartOrResumeAsync(ct, locationId);
            await _preparation.RandomizeAsync(ct);

            var confirmed = await _preparation.ConfirmAsync(ct); // writes shelf state + advances to Sales
            if (!confirmed)
            {
                Debug.LogWarning($"{LogPrefix} preparation confirm rejected — falling back to hub.");
                return false;
            }

            // Additively load the location over the hub. Raises LocationLoadedChanged → triggers tutorial_day_1.
            await _gameFlow.EnterLocationAsync(ct);

            Debug.Log($"{LogPrefix} entered location '{locationId}' directly with an auto-stocked shelf.");
            return true;
        }

        // Day 1 = single fixed location: the first unlocked one in catalog order (mirror of
        // GameplaySceneController.PickFirstUnlockedLocationId).
        private string ResolveFixedLocationId()
        {
            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                if (_locationUnlock == null || _locationUnlock.IsUnlocked(config.Id))
                    return config.Id;
            }

            return null;
        }
    }
}
