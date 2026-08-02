using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.LocationUnlock.API;
using Game.SalesStats.API;
using Save;
using UnityEngine;

namespace Game.LocationUnlock.Services
{
    /// <summary>
    /// Default <see cref="ILocationUnlockService"/>. Locations without UnlockCost open automatically
    /// when their conditions are met; locations with UnlockCost require an explicit player unlock that
    /// consumes inventory items.
    /// </summary>
    public sealed class LocationUnlockService : ILocationUnlockService, ISaveHook
    {
        private const string LogPrefix = "[LocationUnlock]";

        private readonly ILocationUnlockRepository _repository;
        private readonly IConfigsService _configs;
        private readonly IInventoryService _inventory;
        private readonly LocationUnlockConditionBuilder _conditionBuilder;

        private readonly Dictionary<string, ICondition> _conditions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationUnlockCostConfig[]> _costs = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);

        private bool _loaded;

        public LocationUnlockService(
            ISaveService save,
            ILocationUnlockRepository repository,
            IConfigsService configs,
            IConditionParser parser,
            ISalesStatsService salesStats,
            IInventoryService inventory = null)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _inventory = inventory;
            _conditionBuilder = new LocationUnlockConditionBuilder(
                parser ?? throw new ArgumentNullException(nameof(parser)));

            save.RegisterHook(this);

            if (salesStats != null) salesStats.Changed += _ => OnConditionDataChanged();
        }

        public event Action<string> Unlocked;
        public event Action<string> StatusChanged;

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);
            _unlocked.Clear();
            if (dto?.UnlockedIds != null)
                foreach (var id in dto.UnlockedIds)
                    if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);

            BuildCatalog();

            var newlyUnlocked = CollectNewlyUnlocked();
            if (newlyUnlocked != null)
                await _repository.SaveAsync(BuildDto(), ct);

            _loaded = true;

            if (newlyUnlocked != null)
                foreach (var id in newlyUnlocked)
                    Unlocked?.Invoke(id);

            Debug.Log($"{LogPrefix} loaded: {_unlocked.Count} unlocked of {_conditions.Count} locations.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        public bool IsUnlocked(string locationId)
            => !string.IsNullOrEmpty(locationId) && _unlocked.Contains(locationId);

        public LocationUnlockStatus GetStatus(string locationId)
        {
            if (string.IsNullOrEmpty(locationId) || !_conditions.ContainsKey(locationId))
            {
                Debug.LogWarning($"{LogPrefix} GetStatus for unknown location '{locationId}'.");
                return new LocationUnlockStatus(locationId, LocationUnlockState.Locked,
                    ConditionResult.Boolean(false, "unknown.location"));
            }

            var state = ComputeState(locationId, out var progress);
            return new LocationUnlockStatus(locationId, state, progress);
        }

        public IReadOnlyList<LocationUnlockCostProgress> GetCost(string locationId)
        {
            if (string.IsNullOrEmpty(locationId) || !_costs.TryGetValue(locationId, out var cost) || cost.Length == 0)
                return Array.Empty<LocationUnlockCostProgress>();

            var result = new List<LocationUnlockCostProgress>(cost.Length);
            for (var i = 0; i < cost.Length; i++)
            {
                var item = cost[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId) || item.Amount <= 0) continue;
                result.Add(new LocationUnlockCostProgress(
                    item.ItemId,
                    _inventory != null ? _inventory.GetCount(item.ItemId) : 0,
                    item.Amount));
            }
            return result;
        }

        public async UniTask<UnlockResult> TryUnlockAsync(string locationId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(locationId) || !_conditions.ContainsKey(locationId))
                return UnlockResult.UnknownLocation;

            if (_unlocked.Contains(locationId))
                return UnlockResult.AlreadyUnlocked;

            if (!_conditions[locationId].Evaluate().IsMet)
                return UnlockResult.ConditionsNotMet;

            if (!HasAllCostItems(locationId))
                return UnlockResult.NotEnoughItems;

            if (!await ConsumeCostAsync(locationId, ct))
                return UnlockResult.NotEnoughItems;

            _unlocked.Add(locationId);
            await _repository.SaveAsync(BuildDto(), ct);

            Debug.Log($"{LogPrefix} unlocked '{locationId}'.");
            Unlocked?.Invoke(locationId);
            return UnlockResult.Ok;
        }

        private void OnConditionDataChanged()
        {
            if (!_loaded) return;

            var newlyUnlocked = CollectNewlyUnlocked(out var stillLocked);

            if (newlyUnlocked != null)
            {
                _repository.SaveAsync(BuildDto(), CancellationToken.None).Forget();
                foreach (var id in newlyUnlocked)
                {
                    Debug.Log($"{LogPrefix} auto-unlocked '{id}' (conditions met).");
                    Unlocked?.Invoke(id);
                }
            }

            if (stillLocked != null)
                foreach (var id in stillLocked)
                    StatusChanged?.Invoke(id);
        }

        private List<string> CollectNewlyUnlocked() => CollectNewlyUnlocked(out _);

        private List<string> CollectNewlyUnlocked(out List<string> stillLocked)
        {
            List<string> newlyUnlocked = null;
            stillLocked = null;

            foreach (var id in _conditions.Keys)
            {
                if (_unlocked.Contains(id)) continue;
                if (HasCost(id))
                {
                    (stillLocked ??= new List<string>()).Add(id);
                    continue;
                }

                if (_conditions[id].Evaluate().IsMet)
                {
                    _unlocked.Add(id);
                    (newlyUnlocked ??= new List<string>()).Add(id);
                }
                else
                {
                    (stillLocked ??= new List<string>()).Add(id);
                }
            }

            return newlyUnlocked;
        }

        private LocationUnlockState ComputeState(string locationId, out ConditionResult progress)
        {
            progress = _conditions[locationId].Evaluate();
            return _unlocked.Contains(locationId) ? LocationUnlockState.Unlocked : LocationUnlockState.Locked;
        }

        private void BuildCatalog()
        {
            _conditions.Clear();
            _costs.Clear();

            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                _conditions[config.Id] = _conditionBuilder.Build(config);
                if (config.UnlockCost != null && config.UnlockCost.Length > 0)
                    _costs[config.Id] = config.UnlockCost;
            }
        }

        private bool HasCost(string locationId)
            => !string.IsNullOrEmpty(locationId)
               && _costs.TryGetValue(locationId, out var cost)
               && cost != null
               && cost.Length > 0;

        private bool HasAllCostItems(string locationId)
        {
            if (!HasCost(locationId)) return true;
            if (_inventory == null) return false;

            var cost = _costs[locationId];
            for (var i = 0; i < cost.Length; i++)
            {
                var item = cost[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId) || item.Amount <= 0) continue;
                if (_inventory.GetCount(item.ItemId) < item.Amount) return false;
            }
            return true;
        }

        private async UniTask<bool> ConsumeCostAsync(string locationId, CancellationToken ct)
        {
            if (!HasCost(locationId)) return true;
            if (_inventory == null) return false;

            var cost = _costs[locationId];
            for (var i = 0; i < cost.Length; i++)
            {
                var item = cost[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId) || item.Amount <= 0) continue;
                if (!await _inventory.RemoveAsync(item.ItemId, item.Amount, ct))
                    return false;
            }
            return true;
        }

        private LocationUnlockStateDto BuildDto()
            => new LocationUnlockStateDto { UnlockedIds = new List<string>(_unlocked) };
    }
}
