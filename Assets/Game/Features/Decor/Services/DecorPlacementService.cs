using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Save;
using UnityEngine;

namespace Game.Decor.Services
{
    /// <summary>
    /// Persistent placement of decor items into LocationConfig slots. Self-registers as
    /// <see cref="ISaveHook"/> in the constructor; <c>AfterLoadAsync</c> loads state and runs
    /// orphan cleanup (placements referencing decor or slots that no longer exist are dropped
    /// with a warning). Phase 0 hardcodes the location id <c>"loc_downtown"</c>.
    /// </summary>
    public sealed class DecorPlacementService : IDecorPlacementService, ISaveHook
    {
        private const string LogTag = "[DecorPlacement]";
        public const string HardcodedLocationId = "loc_downtown";

        private readonly SaveBackedDecorPlacementStorage _storage;
        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;

        private DecorPlacementState _state = new();
        private bool _loaded;

        public event Action PlacementChanged;

        public DecorPlacementService(
            SaveBackedDecorPlacementStorage storage,
            IInventoryService inventory,
            IConfigsService configs,
            ISaveService save)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            save?.RegisterHook(this);
        }

        // Exposed only for the IDecorRewardService neighbour; not part of the public API.
        internal DecorPlacementState State => _state;
        internal UniTask PersistAsync(CancellationToken ct) => _storage.SaveAsync(_state, ct);

        public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => _state.Placements;

        public string GetDecorInSlot(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) return null;
            for (var i = 0; i < _state.Placements.Count; i++)
            {
                if (string.Equals(_state.Placements[i].SlotId, slotId, StringComparison.OrdinalIgnoreCase))
                    return _state.Placements[i].DecorId;
            }
            return null;
        }

        public IReadOnlyList<string> GetActiveDecorIds()
        {
            var result = new string[_state.Placements.Count];
            for (var i = 0; i < _state.Placements.Count; i++)
                result[i] = _state.Placements[i].DecorId;
            return result;
        }

        public async UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(decorId) || string.IsNullOrEmpty(slotId))
                return DecorPlacementResult.SlotNotFound;

            if (!_inventory.Has(decorId))
                return DecorPlacementResult.DecorNotInInventory;

            var decorConfig = _configs.Get<DecorConfig>(decorId);
            if (decorConfig == null)
                return DecorPlacementResult.DecorConfigMissing;

            var slot = FindSlot(slotId);
            if (slot == null)
                return DecorPlacementResult.SlotNotFound;

            if (decorConfig.PositionType != slot.PositionType)
                return DecorPlacementResult.PositionTypeMismatch;

            if ((int)decorConfig.Size > (int)slot.MaxSize)
                return DecorPlacementResult.SizeMismatch;

            if (!string.IsNullOrEmpty(GetDecorInSlot(slotId)))
                return DecorPlacementResult.SlotOccupied;

            _state.Placements.Add(new DecorPlacementEntry { SlotId = slotId, DecorId = decorId });
            await _storage.SaveAsync(_state, ct);
            PlacementChanged?.Invoke();
            return DecorPlacementResult.Success;
        }

        public async UniTask UnplaceAsync(string slotId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(slotId)) return;
            var removed = _state.Placements.RemoveAll(p =>
                string.Equals(p.SlotId, slotId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return;
            await _storage.SaveAsync(_state, ct);
            PlacementChanged?.Invoke();
        }

        public async UniTask ClearAllAsync(CancellationToken ct)
        {
            if (_state.Placements.Count == 0) return;
            _state.Placements.Clear();
            await _storage.SaveAsync(_state, ct);
            PlacementChanged?.Invoke();
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            _state = await _storage.LoadAsync(ct);
            _loaded = true;

            // Orphan cleanup: drop placements whose decor or slot disappeared from configs.
            var dirty = false;
            for (var i = _state.Placements.Count - 1; i >= 0; i--)
            {
                var entry = _state.Placements[i];
                if (entry == null || string.IsNullOrEmpty(entry.DecorId) || string.IsNullOrEmpty(entry.SlotId))
                {
                    Debug.LogWarning($"{LogTag} Dropping invalid placement entry at index {i}.");
                    _state.Placements.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                var decorConfig = _configs.Get<DecorConfig>(entry.DecorId);
                if (decorConfig == null)
                {
                    Debug.LogWarning($"{LogTag} Decor '{entry.DecorId}' is missing from configs — unplacing from slot '{entry.SlotId}'.");
                    _state.Placements.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                var slot = FindSlot(entry.SlotId);
                if (slot == null)
                {
                    Debug.LogWarning($"{LogTag} Slot '{entry.SlotId}' is missing from location config — dropping placement of '{entry.DecorId}'.");
                    _state.Placements.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                if (decorConfig.PositionType != slot.PositionType)
                {
                    Debug.LogWarning($"{LogTag} Decor '{entry.DecorId}' position type changed and no longer fits slot '{entry.SlotId}' — dropping.");
                    _state.Placements.RemoveAt(i);
                    dirty = true;
                }
            }

            if (dirty)
            {
                await _storage.SaveAsync(_state, ct);
                PlacementChanged?.Invoke();
            }
        }

        private DecorSlot FindSlot(string slotId)
        {
            var location = _configs.Get<LocationConfig>(HardcodedLocationId);
            if (location?.DecorSlots == null) return null;
            for (var i = 0; i < location.DecorSlots.Length; i++)
            {
                var slot = location.DecorSlots[i];
                if (slot != null && string.Equals(slot.Id, slotId, StringComparison.OrdinalIgnoreCase))
                    return slot;
            }
            return null;
        }
    }
}
