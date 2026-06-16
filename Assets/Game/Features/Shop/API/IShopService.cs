using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Shop.API
{
    /// <summary>
    /// Public Shop entry point. Phase 0: backed by a local config (<c>shop.json</c>) and local
    /// resources/inventory writes through <see cref="Rewards.API.IRewardGrantService"/>. Phase 2+:
    /// implementation swaps to a server-driven catalog and <c>POST /shop/purchase</c> calls; the
    /// contract stays the same.
    /// </summary>
    public interface IShopService
    {
        IReadOnlyList<ShopLot> GetLots(string storefrontId);

        bool TryGetLot(string lotId, out ShopLot lot);

        /// <summary>Current persisted purchase counter (0 if never bought).</summary>
        int GetPurchaseCount(string lotId);

        /// <summary>
        /// True when the lot can be bought right now (i.e. not exhausted for Disposable lots).
        /// Does NOT check currency balance — currency is checked inside <see cref="BuyAsync"/>.
        /// </summary>
        bool IsAvailable(string lotId);

        /// <summary>
        /// Runs the Phase 0 purchase pipeline: limit check → currency check → currency remove →
        /// reward grant → persist → event. Non-success returns leave player state untouched on the
        /// branches that haven't yet mutated (see <c>SHOP.md §12.4</c> for the atomicity note).
        /// </summary>
        UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct);

        event Action<ShopPurchaseEvent> LotPurchased;
    }
}
