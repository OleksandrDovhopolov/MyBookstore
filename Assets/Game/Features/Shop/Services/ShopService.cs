using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Inventory.API;
using Game.Resources.API;
using Game.Rewards.API;
using Game.Shop.API;
using Save;
using UnityEngine;

namespace Game.Shop.Services
{
    /// <summary>
    /// Phase 0 <see cref="IShopService"/> implementation. Self-registers as <see cref="ISaveHook"/>:
    /// on AfterLoadAsync it loads persisted purchase counts and warms an in-memory catalog from
    /// <see cref="IConfigsService.GetAll{ShopConfig}"/>. <see cref="BuyAsync"/> runs the local
    /// purchase pipeline; Phase 2+ swaps the implementation behind the same contract.
    /// </summary>
    public sealed class ShopService : IShopService, ISaveHook
    {
        private const string LogPrefix = "[Shop]";
        private const string SourcePrefix = "shop:";

        private readonly ISaveService _save;
        private readonly SaveBackedShopRepository _repository;
        private readonly IResourcesService _resources;
        private readonly IRewardGrantService _rewards;
        private readonly IConfigsService _configs;
        private readonly IInventoryService _inventory;

        private readonly Dictionary<string, ShopLot> _lotsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ShopLot>> _lotsByStorefront = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RewardSpec> _specsByLotId = new(StringComparer.Ordinal);

        private ShopStateDto _state = new ShopStateDto();
        private bool _loaded;

        public ShopService(
            ISaveService save,
            SaveBackedShopRepository repository,
            IResourcesService resources,
            IRewardGrantService rewards,
            IConfigsService configs,
            IInventoryService inventory)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            save.RegisterHook(this);
        }

        public event Action<ShopPurchaseEvent> LotPurchased;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            // PR6: configs are now guaranteed warm before save load runs (Bootstrap.cs split the
            // former parallel phase_data group). The local _configs.WarmupAsync hedge from PR3 is
            // gone; configs are always ready when this hook fires.

            _state = await _repository.LoadAsync(ct) ?? new ShopStateDto();
            if (_state.Lots == null) _state.Lots = new Dictionary<string, LotPurchasesDto>(StringComparer.Ordinal);

            await TryMigrateLegacyDecorAsync(ct);

            WarmupCatalog();
            _loaded = true;

            Debug.Log($"{LogPrefix} loaded: {_lotsById.Count} lots across {_lotsByStorefront.Count} storefronts.");
        }

        // ----- legacy migration -----

        /// <summary>
        /// One-shot migration of the pre-Shop decor flags (<c>DecorPlacementState.FirstDayRewardClaimed</c>
        /// and <c>FirstDayPurchaseDone</c>) into <see cref="ShopStateDto"/>. Reads the legacy DTO directly
        /// through <see cref="ISaveService"/> (not through <c>DecorPlacementService</c>) so hook ordering
        /// inside VContainer doesn't matter. Idempotent: bails out if the shop state already contains
        /// any decor lot entry. Legacy fields stay in <c>decor.placement</c> as a rollback safety net.
        /// </summary>
        private async UniTask TryMigrateLegacyDecorAsync(CancellationToken ct)
        {
            if (_state.Lots.ContainsKey(NewspaperShopLotIds.DecorFreeVintageGlobe)
                || _state.Lots.ContainsKey(NewspaperShopLotIds.DecorPaidCoffeePot))
                return;

            var legacy = await _save.GetModuleAsync<DecorPlacementState>(DecorSaveKeys.Placement, ct);
            if (legacy == null) return;

            var dirty = false;
            if (legacy.FirstDayRewardClaimed)
            {
                _state.Lots[NewspaperShopLotIds.DecorFreeVintageGlobe] = new LotPurchasesDto { Purchases = 1 };
                dirty = true;
            }
            if (legacy.FirstDayPurchaseDone)
            {
                _state.Lots[NewspaperShopLotIds.DecorPaidCoffeePot] = new LotPurchasesDto { Purchases = 1 };
                dirty = true;
            }

            if (dirty)
            {
                await _repository.SaveAsync(_state, ct);
                Debug.Log($"{LogPrefix} migrated legacy decor flags (free={legacy.FirstDayRewardClaimed}, paid={legacy.FirstDayPurchaseDone}).");
            }
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- sync read -----

        public IReadOnlyList<ShopLot> GetLots(string storefrontId)
        {
            if (string.IsNullOrEmpty(storefrontId)) return Array.Empty<ShopLot>();
            return _lotsByStorefront.TryGetValue(storefrontId, out var lots)
                ? lots
                : (IReadOnlyList<ShopLot>)Array.Empty<ShopLot>();
        }

        public bool TryGetLot(string lotId, out ShopLot lot)
        {
            if (string.IsNullOrEmpty(lotId)) { lot = null; return false; }
            return _lotsById.TryGetValue(lotId, out lot);
        }

        public int GetPurchaseCount(string lotId)
        {
            if (string.IsNullOrEmpty(lotId)) return 0;
            return _state.Lots != null && _state.Lots.TryGetValue(lotId, out var dto) ? dto.Purchases : 0;
        }

        public bool IsAvailable(string lotId)
        {
            if (!TryGetLot(lotId, out var lot)) return false;
            if (!IsWithinLimit(lotId, lot)) return false;
            if (HasOwnedInlineRewardItem(lotId)) return false;
            return true;
        }

        private bool IsWithinLimit(string lotId, ShopLot lot)
        {
            if (lot.Limit.Mode == ShopLimitMode.Unlimited) return true;
            var cap = lot.Limit.MaxPurchases ?? int.MaxValue;  // null → effectively unlimited (defensive)
            return GetPurchaseCount(lotId) < cap;
        }

        /// <summary>
        /// True if the lot's inline <c>rewardItems</c> contain any <see cref="RewardKind.InventoryItem"/>
        /// the player already owns. Phase 1 simple dupe guard: book/decor categories are Unique-mode,
        /// so <c>IInventoryService.Has(id)</c> means «already owned». Book-box lots ship with empty
        /// rewardItems (expander fills at grant time), so this check is a no-op for them — their own
        /// owned-filter logic lives inside <c>BookBoxRewardExpander</c>.
        /// </summary>
        private bool HasOwnedInlineRewardItem(string lotId)
        {
            if (!_specsByLotId.TryGetValue(lotId, out var spec) || spec.Items == null) return false;
            for (var i = 0; i < spec.Items.Count; i++)
            {
                var item = spec.Items[i];
                if (item.Kind == RewardKind.InventoryItem && _inventory.Has(item.Id))
                    return true;
            }
            return false;
        }

        // ----- async write -----

        public async UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct)
        {
            if (!_loaded)
                Debug.LogWarning($"{LogPrefix} BuyAsync before AfterLoadAsync; attempt will proceed but state may be incomplete.");

            if (!TryGetLot(lotId, out var lot))
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.LotNotFound);

            // AlreadyOwned takes priority over LimitReached so UI can display a clear reason — both
            // collapse into IsAvailable=false but their causes (and player-facing messages) differ.
            if (HasOwnedInlineRewardItem(lotId))
            {
                Debug.LogWarning($"{LogPrefix} Lot '{lotId}' grants an item already owned. Purchase blocked.");
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.AlreadyOwned, lot);
            }

            if (!IsWithinLimit(lotId, lot))
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.LimitReached, lot);

            var price = lot.Price;
            if (price.Amount > 0)
            {
                if (!_resources.Has(price.Currency, price.Amount))
                    return ShopPurchaseResult.Fail(ShopPurchaseStatus.NotEnoughCurrency, lot);

                var removed = await _resources.RemoveAsync(price.Currency, price.Amount, SourcePrefix + lotId, ct);
                if (!removed)
                    return ShopPurchaseResult.Fail(ShopPurchaseStatus.NotEnoughCurrency, lot);
            }

            if (!_specsByLotId.TryGetValue(lotId, out var spec))
            {
                Debug.LogError($"{LogPrefix} Missing RewardSpec for lot '{lotId}' (rewardId='{lot.RewardId}'). " +
                               "Gold has been charged but nothing was granted — manual recovery needed.");
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.InternalError, lot);
            }

            var grant = await _rewards.GrantAsync(spec, SourcePrefix + lotId, ct);
            if (!grant.Success)
            {
                Debug.LogError($"{LogPrefix} Grant failed for lot '{lotId}': {grant.FailureReason}. " +
                               "Gold has been charged but reward was not delivered.");
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.InternalError, lot);
            }

            IncrementPurchase(lotId);
            await _repository.SaveAsync(_state, ct);

            var evt = new ShopPurchaseEvent(lot, grant.Granted);
            LotPurchased?.Invoke(evt);
            return ShopPurchaseResult.Ok(lot, grant.Granted);
        }

        // ----- internals -----

        private void IncrementPurchase(string lotId)
        {
            if (!_state.Lots.TryGetValue(lotId, out var dto))
            {
                dto = new LotPurchasesDto();
                _state.Lots[lotId] = dto;
            }
            dto.Purchases++;
        }

        private void WarmupCatalog()
        {
            _lotsById.Clear();
            _lotsByStorefront.Clear();
            _specsByLotId.Clear();

            var configs = _configs.GetAll<ShopConfig>();
            if (configs == null) return;

            foreach (var cfg in configs)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;

                var price = new ShopPrice(cfg.Price?.Currency, cfg.Price?.Amount ?? 0);
                var limit = cfg.Limit != null
                    ? new ShopLotLimit(cfg.Limit.Mode, cfg.Limit.MaxPurchases)
                    : ShopLotLimit.Unlimited();

                var lot = new ShopLot(cfg.Id, cfg.StorefrontId, price, cfg.RewardId ?? cfg.Id, limit);
                _lotsById[lot.LotId] = lot;

                if (!_lotsByStorefront.TryGetValue(lot.StorefrontId, out var list))
                {
                    list = new List<ShopLot>();
                    _lotsByStorefront[lot.StorefrontId] = list;
                }
                list.Add(lot);

                _specsByLotId[lot.LotId] = BuildSpec(cfg, lot);
            }
        }

        private static RewardSpec BuildSpec(ShopConfig cfg, ShopLot lot)
        {
            var items = cfg.RewardItems;
            if (items == null || items.Length == 0)
                return new RewardSpec(lot.RewardId, Array.Empty<RewardItem>());

            var rewardItems = new RewardItem[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var src = items[i];
                rewardItems[i] = src.Kind == RewardKind.Resource
                    ? RewardItem.Resource(src.Id, src.Amount)
                    : RewardItem.InventoryItem(src.Id, src.Category, src.Amount);
            }
            return new RewardSpec(lot.RewardId, rewardItems);
        }
    }
}
