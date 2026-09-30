using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Inventory.API;
using Game.Localization;
using Game.Resources.API;
using Game.Rewards.API;
using Game.Shop.API;
using Newtonsoft.Json.Linq;
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
        private readonly IShopRewardSpecProvider _rewardSpecs;
        private readonly IConfigsService _configs;
        private readonly IInventoryService _inventory;
        private readonly ICurrentDayProvider _dayProvider;
        private readonly ILocalizationService _localization;
        private readonly Func<IConditionParser> _conditionParser;

        private readonly Dictionary<string, ShopLot> _lotsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ShopLot>> _lotsByStorefront = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ICondition> _unlockById = new(StringComparer.Ordinal);

        private ShopStateDto _state = new ShopStateDto();
        private IReadOnlyList<ShopLot> _offeredBooks;
        private int _offeredBooksDay = int.MinValue;
        private bool _loaded;

        public ShopService(
            ISaveService save,
            SaveBackedShopRepository repository,
            IResourcesService resources,
            IRewardGrantService rewards,
            IShopRewardSpecProvider rewardSpecs,
            IConfigsService configs,
            IInventoryService inventory,
            ICurrentDayProvider dayProvider,
            ILocalizationService localization = null,
            Func<IConditionParser> conditionParser = null)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _rewardSpecs = rewardSpecs ?? throw new ArgumentNullException(nameof(rewardSpecs));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _dayProvider = dayProvider ?? throw new ArgumentNullException(nameof(dayProvider));
            _localization = localization;
            // Resolved lazily inside WarmupCatalog: ShopService is built at scope start (the
            // IStartable analytics listener depends on IShopService), so injecting IConditionParser
            // directly would drag every condition factory - and the ISaveHook each one registers in
            // its own constructor - into that moment, silently reordering the global save-hook order.
            _conditionParser = conditionParser;

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

        public IReadOnlyList<ShopLot> GetOfferedLots(string storefrontId)
            => FilterUnlocked(GetRotatedLots(storefrontId));

        /// <summary>
        /// The storefront before progression gating: book boxes rotate by day, every other storefront
        /// offers its whole catalog. Kept separate so the day-keyed rotation cache never wraps the
        /// unlock filter.
        /// </summary>
        private IReadOnlyList<ShopLot> GetRotatedLots(string storefrontId)
        {
            if (!string.Equals(storefrontId, NewspaperShopLotIds.StorefrontBooks, StringComparison.Ordinal))
                return GetLots(storefrontId);

            var day = _dayProvider.CurrentDay;
            if (_offeredBooks != null && _offeredBooksDay == day)
                return _offeredBooks;

            _offeredBooks = BookBoxRotation.Select(GetLots(storefrontId), day);
            _offeredBooksDay = day;
            return _offeredBooks;
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
            if (!IsOffered(lot)) return false;
            if (!IsWithinLimit(lotId, lot)) return false;
            if (!_rewardSpecs.TryBuild(lot, out var spec)) return false;
            if (HasOwnedInlineRewardItem(spec)) return false;
            return true;
        }

        private bool IsWithinLimit(string lotId, ShopLot lot)
        {
            if (lot.Limit.Mode == ShopLimitMode.Unlimited) return true;
            var cap = lot.Limit.MaxPurchases ?? int.MaxValue;  // null → effectively unlimited (defensive)
            if (cap <= 0) return false;

            if (lot.Limit.Mode == ShopLimitMode.Daily)
            {
                var dto = GetPurchaseDto(lotId);
                return dto == null
                       || dto.LastPurchasedDay != _dayProvider.CurrentDay
                       || dto.PurchasesToday < cap;
            }

            return GetPurchaseCount(lotId) < cap;
        }

        /// <summary>
        /// True if the lot's inline <c>rewardItems</c> contain any <see cref="RewardKind.InventoryItem"/>
        /// the player already owns. Phase 1 simple dupe guard: book/decor categories are Unique-mode,
        /// so <c>IInventoryService.Has(id)</c> means «already owned». Book-box lots ship with empty
        /// rewardItems (expander fills at grant time), so this check is a no-op for them — their own
        /// owned-filter logic lives inside <c>BookBoxRewardExpander</c>.
        /// </summary>
        private bool HasOwnedInlineRewardItem(RewardSpec spec)
        {
            if (spec == null || spec.Items == null) return false;
            for (var i = 0; i < spec.Items.Count; i++)
            {
                var item = spec.Items[i];
                if (item.Kind == RewardKind.InventoryItem
                    && IsDuplicateGuardedCategory(item.Category)
                    && _inventory.Has(item.Id))
                    return true;
            }
            return false;
        }

        private static bool IsDuplicateGuardedCategory(string categoryId)
            => string.Equals(categoryId, InventoryCategories.Book, StringComparison.Ordinal)
               || string.Equals(categoryId, InventoryCategories.Decor, StringComparison.Ordinal)
               || string.Equals(categoryId, InventoryCategories.QuestItem, StringComparison.Ordinal);

        // ----- async write -----

        public async UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct)
        {
            if (!_loaded)
                Debug.LogWarning($"{LogPrefix} BuyAsync before AfterLoadAsync; attempt will proceed but state may be incomplete.");

            if (!TryGetLot(lotId, out var lot))
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.LotNotFound);

            if (!IsOffered(lot))
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.NotOffered, lot);

            if (!_rewardSpecs.TryBuild(lot, out var spec))
            {
                Debug.LogError($"{LogPrefix} Missing RewardSpec for lot '{lotId}' (rewardId='{lot.RewardId}'). " +
                               "Purchase was blocked before charging currency.");
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.InternalError, lot);
            }

            // AlreadyOwned takes priority over LimitReached so UI can display a clear reason — both
            // collapse into IsAvailable=false but their causes (and player-facing messages) differ.
            if (HasOwnedInlineRewardItem(spec))
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

            var grant = await _rewards.GrantAsync(spec, SourcePrefix + lotId, ct);
            if (!grant.Success)
            {
                Debug.LogError($"{LogPrefix} Grant failed for lot '{lotId}': {grant.FailureReason}. " +
                               "Gold has been charged but reward was not delivered.");
                return ShopPurchaseResult.Fail(ShopPurchaseStatus.InternalError, lot);
            }

            IncrementPurchase(lotId, lot);
            await _repository.SaveAsync(_state, ct);

            var evt = new ShopPurchaseEvent(lot, grant.Granted);
            LotPurchased?.Invoke(evt);
            return ShopPurchaseResult.Ok(lot, grant.Granted);
        }

        // ----- internals -----

        private void IncrementPurchase(string lotId, ShopLot lot)
        {
            if (!_state.Lots.TryGetValue(lotId, out var dto))
            {
                dto = new LotPurchasesDto();
                _state.Lots[lotId] = dto;
            }

            if (lot.Limit.Mode == ShopLimitMode.Daily)
            {
                var day = _dayProvider.CurrentDay;
                if (dto.LastPurchasedDay != day)
                {
                    dto.LastPurchasedDay = day;
                    dto.PurchasesToday = 0;
                }

                dto.PurchasesToday++;
            }

            dto.Purchases++;
        }

        private void WarmupCatalog()
        {
            _lotsById.Clear();
            _lotsByStorefront.Clear();
            _unlockById.Clear();
            _offeredBooks = null;
            _offeredBooksDay = int.MinValue;

            var configs = _configs.GetAll<ShopConfig>();
            if (configs == null) return;

            foreach (var cfg in configs)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;

                var price = new ShopPrice(cfg.Price?.Currency, cfg.Price?.Amount ?? 0);
                var limit = cfg.Limit != null
                    ? new ShopLotLimit(cfg.Limit.Mode, cfg.Limit.MaxPurchases)
                    : ShopLotLimit.Unlimited();

                var lot = new ShopLot(
                    cfg.Id,
                    cfg.StorefrontId,
                    price,
                    cfg.RewardId ?? cfg.Id,
                    limit,
                    ResolveText(cfg.DisplayNameKey, cfg.Id),
                    ResolveText(cfg.DescriptionKey, string.Empty));
                _lotsById[lot.LotId] = lot;

                // No unlock node means always available (same convention as LocationConfig.Unlock).
                if (cfg.Unlock is { HasValues: true })
                    _unlockById[lot.LotId] = ParseUnlock(lot.LotId, cfg.Unlock);

                if (!_lotsByStorefront.TryGetValue(lot.StorefrontId, out var list))
                {
                    list = new List<ShopLot>();
                    _lotsByStorefront[lot.StorefrontId] = list;
                }
                list.Add(lot);
            }
        }

        private LotPurchasesDto GetPurchaseDto(string lotId)
        {
            if (string.IsNullOrEmpty(lotId) || _state.Lots == null) return null;
            return _state.Lots.TryGetValue(lotId, out var dto) ? dto : null;
        }

        private bool IsOffered(ShopLot lot)
        {
            if (lot == null) return false;

            // Progression gate first: an O(1) dictionary hit plus a struct evaluate, so IsAvailable stays
            // allocation-free. Scanning GetOfferedLots instead would build a filtered list on every call.
            if (!IsUnlocked(lot.LotId)) return false;

            if (!string.Equals(lot.StorefrontId, NewspaperShopLotIds.StorefrontBooks, StringComparison.Ordinal))
                return true;

            var rotated = GetRotatedLots(lot.StorefrontId);
            for (var i = 0; i < rotated.Count; i++)
            {
                if (string.Equals(rotated[i]?.LotId, lot.LotId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>True when the lot declares no unlock condition, or its condition is currently met.</summary>
        private bool IsUnlocked(string lotId)
        {
            if (string.IsNullOrEmpty(lotId)) return false;
            if (!_unlockById.TryGetValue(lotId, out var condition)) return true;
            return condition.Evaluate().IsMet;
        }

        /// <summary>
        /// Drops lots whose unlock condition is not met. Deliberately <b>not</b> cached: a lot gated on
        /// visitLocation has to appear the moment the player visits, and the only cache in this class is
        /// keyed on the day, which a visit does not change. Leaf conditions are struct-based and
        /// allocation-free, so evaluating per call is cheaper than a stale-cache bug. Returns the source
        /// list unchanged when nothing is filtered out.
        /// </summary>
        private IReadOnlyList<ShopLot> FilterUnlocked(IReadOnlyList<ShopLot> lots)
        {
            if (lots == null) return Array.Empty<ShopLot>();
            if (lots.Count == 0 || _unlockById.Count == 0) return lots;

            List<ShopLot> filtered = null;
            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot != null && IsUnlocked(lot.LotId))
                {
                    filtered?.Add(lot);
                    continue;
                }

                // First rejection: materialise everything kept so far, then keep appending.
                filtered ??= CopyFirst(lots, i);
            }

            return filtered ?? lots;
        }

        private static List<ShopLot> CopyFirst(IReadOnlyList<ShopLot> source, int count)
        {
            var list = new List<ShopLot>(source.Count);
            for (var i = 0; i < count; i++)
                list.Add(source[i]);
            return list;
        }

        /// <summary>
        /// Fail-closed exactly like <c>ConditionParser</c> itself: a lot that declares an unlock we cannot
        /// parse stays hidden rather than silently becoming free content.
        /// </summary>
        private ICondition ParseUnlock(string lotId, JObject node)
        {
            var parser = _conditionParser?.Invoke();
            if (parser != null) return parser.Parse(node);

            Debug.LogError($"{LogPrefix} lot {lotId} declares an unlock condition but no IConditionParser " +
                           "is available. Lot stays hidden.");
            return NeverUnlocked.Instance;
        }

        private sealed class NeverUnlocked : ICondition
        {
            public static readonly NeverUnlocked Instance = new();

            public ConditionResult Evaluate() => ConditionResult.Boolean(false, "shop.unlock.unavailable");
        }

        private string ResolveText(string key, string fallback)
            => !string.IsNullOrEmpty(key)
                ? (_localization != null ? _localization.Get(key) : LocalizationLocator.GetOrKey(key))
                : fallback;
    }
}
