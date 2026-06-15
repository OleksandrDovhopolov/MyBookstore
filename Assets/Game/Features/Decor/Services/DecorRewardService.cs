using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Game.Resources.API;
using UnityEngine;

namespace Game.Decor.Services
{
    /// <summary>
    /// First-day newspaper offers: one free decor + one paid decor for gold.
    /// Phase 0 hardcodes the offer ids. State (claimed / purchased flags) lives inside
    /// DecorPlacementState via the shared <see cref="SaveBackedDecorPlacementStorage"/>.
    /// </summary>
    public sealed class DecorRewardService : IDecorRewardService
    {
        private const string LogTag = "[DecorReward]";
        private const string ReasonNewspaper = "decor_newspaper";

        public const string FreeDecorId = "vintage_globe";
        public const string PaidDecorId = "coffee_pot";
        public const int PaidPrice = 50;

        private readonly DecorPlacementService _placement;
        private readonly IInventoryService _inventory;
        private readonly IResourcesService _resources;

        public DecorRewardService(
            DecorPlacementService placement,
            IInventoryService inventory,
            IResourcesService resources)
        {
            _placement = placement ?? throw new ArgumentNullException(nameof(placement));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public bool HasFreeDecorAvailable => !_placement.State.FirstDayRewardClaimed;
        public bool HasPaidOfferAvailable => !_placement.State.FirstDayPurchaseDone;

        public string OfferedFreeDecorId => FreeDecorId;
        public string OfferedPaidDecorId => PaidDecorId;
        public int OfferedPaidPrice => PaidPrice;

        public async UniTask<bool> ClaimFreeDecorAsync(CancellationToken ct)
        {
            if (_placement.State.FirstDayRewardClaimed) return false;

            await _inventory.AddAsync(FreeDecorId, InventoryCategories.Decor, 1, ct);

            _placement.State.FirstDayRewardClaimed = true;
            await _placement.PersistAsync(ct);
            Debug.Log($"{LogTag} Granted free decor '{FreeDecorId}'.");
            return true;
        }

        public async UniTask<bool> BuyOfferedDecorAsync(CancellationToken ct)
        {
            if (_placement.State.FirstDayPurchaseDone) return false;

            if (!_resources.Has(ResourceIds.Gold, PaidPrice))
            {
                Debug.Log($"{LogTag} Cannot buy '{PaidDecorId}' — not enough gold ({_resources.GetAmount(ResourceIds.Gold)} / {PaidPrice}).");
                return false;
            }

            var removed = await _resources.RemoveAsync(ResourceIds.Gold, PaidPrice, ReasonNewspaper, ct);
            if (!removed) return false;

            await _inventory.AddAsync(PaidDecorId, InventoryCategories.Decor, 1, ct);

            _placement.State.FirstDayPurchaseDone = true;
            await _placement.PersistAsync(ct);
            Debug.Log($"{LogTag} Purchased decor '{PaidDecorId}' for {PaidPrice} gold.");
            return true;
        }
    }
}
