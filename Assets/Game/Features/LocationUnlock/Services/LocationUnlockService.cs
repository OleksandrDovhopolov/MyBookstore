using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.LocationUnlock.API;
using Game.Resources.API;
using Game.SalesStats.API;
using Save;
using UnityEngine;

namespace Game.LocationUnlock.Services
{
    /// <summary>
    /// Default <see cref="ILocationUnlockService"/>. Self-registers as <see cref="ISaveHook"/>:
    /// on <see cref="AfterLoadAsync"/> it loads purchased ids and parses every location's unlock
    /// conditions once (configs are warm by then, same as <c>ShopService</c>). Unlocks are rare, so
    /// the purchase fact is written through on <see cref="TryUnlockAsync"/> (unlike the batched sales
    /// counters). Reacts to <see cref="ISalesStatsService.Changed"/> to re-evaluate not-yet-unlocked
    /// locations and raise <see cref="StatusChanged"/>.
    /// </summary>
    public sealed class LocationUnlockService : ILocationUnlockService, ISaveHook
    {
        private const string LogPrefix = "[LocationUnlock]";

        private readonly ILocationUnlockRepository _repository;
        private readonly IConfigsService _configs;
        private readonly IResourcesService _resources;
        private readonly LocationUnlockConditionBuilder _conditionBuilder;

        private readonly Dictionary<string, ICondition> _conditions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _cost = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationUnlockState> _lastState = new(StringComparer.Ordinal);

        private bool _loaded;

        public LocationUnlockService(
            ISaveService save,
            ILocationUnlockRepository repository,
            IConfigsService configs,
            IConditionParser parser,
            IResourcesService resources,
            ISalesStatsService salesStats)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _conditionBuilder = new LocationUnlockConditionBuilder(
                parser ?? throw new ArgumentNullException(nameof(parser)));

            save.RegisterHook(this);

            // Data sources that feed unlock conditions signal "recompute" here. Adding a new source
            // (reputation, fishing, ...) is one more subscription — the engine stays untouched.
            if (salesStats != null) salesStats.Changed += _ => OnConditionDataChanged();
        }

        public event Action<string> Unlocked;
        public event Action<string> StatusChanged;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);
            _unlocked.Clear();
            if (dto?.UnlockedIds != null)
                foreach (var id in dto.UnlockedIds)
                    if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);

            BuildCatalog();

            _lastState.Clear();
            foreach (var id in _conditions.Keys)
                _lastState[id] = ComputeState(id, out _);

            _loaded = true;
            Debug.Log($"{LogPrefix} loaded: {_unlocked.Count} unlocked of {_conditions.Count} locations.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- sync read -----

        public bool IsUnlocked(string locationId)
            => !string.IsNullOrEmpty(locationId) && _unlocked.Contains(locationId);

        public LocationUnlockStatus GetStatus(string locationId)
        {
            if (string.IsNullOrEmpty(locationId) || !_conditions.ContainsKey(locationId))
            {
                Debug.LogWarning($"{LogPrefix} GetStatus for unknown location '{locationId}'.");
                return new LocationUnlockStatus(locationId, LocationUnlockState.Locked, 0,
                    ConditionResult.Boolean(false, "unknown.location"));
            }

            var state = ComputeState(locationId, out var progress);
            return new LocationUnlockStatus(locationId, state, GetCost(locationId), progress);
        }

        // ----- async write -----

        public async UniTask<UnlockResult> TryUnlockAsync(string locationId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(locationId) || !_conditions.ContainsKey(locationId))
                return UnlockResult.UnknownLocation;

            if (_unlocked.Contains(locationId))
                return UnlockResult.AlreadyUnlocked;

            if (!_conditions[locationId].Evaluate().IsMet)
                return UnlockResult.ConditionsNotMet;

            var cost = GetCost(locationId);
            if (cost > 0)
            {
                if (!_resources.Has(ResourceIds.Gold, cost))
                    return UnlockResult.NotEnoughCurrency;

                var removed = await _resources.RemoveAsync(ResourceIds.Gold, cost, $"unlock_location:{locationId}", ct);
                if (!removed)
                    return UnlockResult.NotEnoughCurrency;
            }

            _unlocked.Add(locationId);
            _lastState[locationId] = LocationUnlockState.Unlocked;
            await _repository.SaveAsync(BuildDto(), ct);

            Debug.Log($"{LogPrefix} unlocked '{locationId}' (cost {cost}).");
            Unlocked?.Invoke(locationId);
            return UnlockResult.Ok;
        }

        // ----- internals -----

        private void OnConditionDataChanged()
        {
            if (!_loaded) return;

            foreach (var id in _conditions.Keys)
            {
                if (_unlocked.Contains(id)) continue;   // Unlocked is terminal.

                var newState = ComputeState(id, out _);
                if (_lastState.TryGetValue(id, out var prev) && prev == newState) continue;

                _lastState[id] = newState;
                StatusChanged?.Invoke(id);
            }
        }

        private LocationUnlockState ComputeState(string locationId, out ConditionResult progress)
        {
            progress = _conditions[locationId].Evaluate();
            if (_unlocked.Contains(locationId)) return LocationUnlockState.Unlocked;
            return progress.IsMet ? LocationUnlockState.Unlockable : LocationUnlockState.Locked;
        }

        private void BuildCatalog()
        {
            _conditions.Clear();
            _cost.Clear();

            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                _conditions[config.Id] = _conditionBuilder.Build(config);
                _cost[config.Id] = config.UnlockCost;
            }
        }

        private int GetCost(string locationId)
            => _cost.TryGetValue(locationId, out var cost) ? cost : 0;

        private LocationUnlockStateDto BuildDto()
            => new LocationUnlockStateDto { UnlockedIds = new List<string>(_unlocked) };
    }
}
