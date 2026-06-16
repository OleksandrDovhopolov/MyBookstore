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
    /// Persistent placement of decor items into BookShopConfig slots. Self-registers as
    /// <see cref="ISaveHook"/> in the constructor; <c>AfterLoadAsync</c> loads state and runs
    /// orphan cleanup (placements referencing decor or slots that no longer exist are dropped
    /// with a warning). Phase 0 hardcodes the shop id <c>"main_bookshop"</c>; a future
    /// IPlayerBookShopProvider (Phase 2+) replaces this const.
    /// </summary>
    public sealed class DecorPlacementService : IDecorPlacementService, ISaveHook
    {
        private const string LogTag = "[DecorPlacement]";
        // TODO Phase 2+: replace with IPlayerBookShopProvider when more than one shop format exists.
        public const string HardcodedBookShopId = "main_bookshop";

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
            Debug.Log($"{LogTag} ctor — service constructed, hook registered. saveNull={(save==null)}");
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

            // Decor is a unique object — refuse to place the same decor into a second slot.
            for (var i = 0; i < _state.Placements.Count; i++)
            {
                if (string.Equals(_state.Placements[i].DecorId, decorId, StringComparison.OrdinalIgnoreCase))
                    return DecorPlacementResult.AlreadyPlaced;
            }

            _state.Placements.Add(new DecorPlacementEntry { SlotId = slotId, DecorId = decorId });
            Debug.Log($"{LogTag} PlaceAsync ENTER save: slot={slotId}, decor={decorId}, totalPlacements={_state.Placements.Count}");
            await _storage.SaveAsync(_state, ct);
            Debug.Log($"{LogTag} PlaceAsync EXIT save: slot={slotId}, decor={decorId} — persisted.");
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

            // Bootstrap runs ConfigsWarmup + SaveDataLoad in parallel (Bootstrap.cs phase_data_load).
            // Orphan cleanup below reads DecorConfig / BookShopConfig — both must be warmed up first,
            // otherwise legitimate placements get silently dropped as "missing from configs".
            // WarmupAsync is idempotent: if configs already warmed, this returns immediately.
            await _configs.WarmupAsync(ct);

            // DBG: snapshot of config readiness after the explicit await — both counts should be > 0.
            var decorsKnown = _configs.GetAll<DecorConfig>().Count;
            var shopsKnown = _configs.GetAll<BookShopConfig>().Count;
            Debug.Log($"{LogTag} AfterLoadAsync ENTER (after WarmupAsync): loaded placements={_state.Placements.Count}, configs decors={decorsKnown}, shops={shopsKnown}");

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
                    Debug.LogWarning($"{LogTag} Decor '{entry.DecorId}' is missing from configs — unplacing from slot '{entry.SlotId}'. (decorsKnown={decorsKnown})");
                    _state.Placements.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                var slot = FindSlot(entry.SlotId);
                if (slot == null)
                {
                    Debug.LogWarning($"{LogTag} Slot '{entry.SlotId}' is missing from bookshop config — dropping placement of '{entry.DecorId}'. (shopsKnown={shopsKnown})");
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

            Debug.Log($"{LogTag} AfterLoadAsync EXIT: final placements={_state.Placements.Count}, dirty(resaved)={dirty}");

            if (dirty)
            {
                await _storage.SaveAsync(_state, ct);
                PlacementChanged?.Invoke();
            }
        }

        private DecorSlot FindSlot(string slotId)
        {
            var shop = _configs.Get<BookShopConfig>(HardcodedBookShopId);
            if (shop?.DecorSlots == null) return null;
            for (var i = 0; i < shop.DecorSlots.Length; i++)
            {
                var slot = shop.DecorSlots[i];
                if (slot != null && string.Equals(slot.Id, slotId, StringComparison.OrdinalIgnoreCase))
                    return slot;
            }
            return null;
        }
    }
}
