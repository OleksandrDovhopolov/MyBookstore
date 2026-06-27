using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.LocationUnlock.API;
using Game.SalesStats.API;
using Save;
using UnityEngine;

namespace Game.LocationUnlock.Services
{
    /// <summary>
    /// Default <see cref="ILocationUnlockService"/>. Self-registers as <see cref="ISaveHook"/>:
    /// on <see cref="AfterLoadAsync"/> it loads already-opened ids and parses every location's unlock
    /// conditions once (configs are warm by then, same as <c>ShopService</c>).
    /// <para>
    /// Unlock is free and automatic: there is no <c>UnlockCost</c> and no "buy" step. The moment a
    /// location's conditions are met the location is opened and persisted — both at load time and
    /// reactively when a data source (<see cref="ISalesStatsService.Changed"/>) moves. Opened locations
    /// stay opened forever (persisted in <see cref="LocationUnlockStateDto.UnlockedIds"/>), even if the
    /// config conditions later change.
    /// </para>
    /// </summary>
    public sealed class LocationUnlockService : ILocationUnlockService, ISaveHook
    {
        private const string LogPrefix = "[LocationUnlock]";

        private readonly ILocationUnlockRepository _repository;
        private readonly IConfigsService _configs;
        private readonly LocationUnlockConditionBuilder _conditionBuilder;

        private readonly Dictionary<string, ICondition> _conditions = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);

        private bool _loaded;

        public LocationUnlockService(
            ISaveService save,
            ILocationUnlockRepository repository,
            IConfigsService configs,
            IConditionParser parser,
            ISalesStatsService salesStats)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
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

            // Auto-unlock anything already satisfied (e.g. a config change lowered a threshold, or the
            // starting location with no conditions). Persist once if the set grew.
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

        // ----- sync read -----

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

        // ----- async write -----

        public async UniTask<UnlockResult> TryUnlockAsync(string locationId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(locationId) || !_conditions.ContainsKey(locationId))
                return UnlockResult.UnknownLocation;

            if (_unlocked.Contains(locationId))
                return UnlockResult.AlreadyUnlocked;

            if (!_conditions[locationId].Evaluate().IsMet)
                return UnlockResult.ConditionsNotMet;

            _unlocked.Add(locationId);
            await _repository.SaveAsync(BuildDto(), ct);

            Debug.Log($"{LogPrefix} unlocked '{locationId}'.");
            Unlocked?.Invoke(locationId);
            return UnlockResult.Ok;
        }

        // ----- internals -----

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

            // Progress of still-locked locations may have moved — let reactive UI refresh.
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
                if (_unlocked.Contains(id)) continue;   // Unlocked is terminal.

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

            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                _conditions[config.Id] = _conditionBuilder.Build(config);
            }
        }

        private LocationUnlockStateDto BuildDto()
            => new LocationUnlockStateDto { UnlockedIds = new List<string>(_unlocked) };
    }
}
