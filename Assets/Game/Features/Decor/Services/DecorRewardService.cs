using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shop.API;

namespace Game.Decor.Services
{
    /// <summary>
    /// Thin facade over <see cref="IShopService"/>. After PR3 the actual purchase state lives in the
    /// Shop save module; this class only exists because <c>UiPilotDebugPanel</c> (and possibly other
    /// debug surfaces) inject <see cref="IDecorRewardService"/> directly. Phase 1 may drop the facade
    /// entirely once all consumers go through <see cref="IShopService"/>.
    /// </summary>
    public sealed class DecorRewardService : IDecorRewardService
    {
        // Item ids are kept here for back-compat — they used to be returned verbatim by
        // OfferedFreeDecorId / OfferedPaidDecorId. They match the inventory item ids inside the
        // shop lots' rewardItems entries.
        private const string FreeItemId = "vintage_globe";
        private const string PaidItemId = "coffee_pot";

        private readonly IShopService _shop;

        public DecorRewardService(IShopService shop)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        }

        public bool HasFreeDecorAvailable => _shop.IsAvailable(NewspaperShopLotIds.DecorFreeVintageGlobe);
        public bool HasPaidOfferAvailable => _shop.IsAvailable(NewspaperShopLotIds.DecorPaidCoffeePot);

        public string OfferedFreeDecorId => FreeItemId;
        public string OfferedPaidDecorId => PaidItemId;

        public int OfferedPaidPrice =>
            _shop.TryGetLot(NewspaperShopLotIds.DecorPaidCoffeePot, out var lot) ? lot.Price.Amount : 0;

        public async UniTask<bool> ClaimFreeDecorAsync(CancellationToken ct)
        {
            var result = await _shop.BuyAsync(NewspaperShopLotIds.DecorFreeVintageGlobe, ct);
            return result.Status == ShopPurchaseStatus.Success;
        }

        public async UniTask<bool> BuyOfferedDecorAsync(CancellationToken ct)
        {
            var result = await _shop.BuyAsync(NewspaperShopLotIds.DecorPaidCoffeePot, ct);
            return result.Status == ShopPurchaseStatus.Success;
        }
    }
}
