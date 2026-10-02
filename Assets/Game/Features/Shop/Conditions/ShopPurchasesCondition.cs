using System;
using Game.Conditions.API;
using Game.Shop.API;

namespace Game.Shop.Conditions
{
    /// <summary>
    /// Leaf condition: the lifetime number of purchases across one or more storefronts is at least
    /// <c>min</c>. Reads <see cref="IShopService.GetPurchaseCount"/>, which is the cumulative counter
    /// (<c>LotPurchasesDto.Purchases</c>) — a Daily lot's per-day allowance lives in a separate field and
    /// does not reset it. The catalog comes from <see cref="IShopService.GetLots"/> rather than
    /// <c>GetOfferedLots</c> so a lot that rotated out or is no longer unlocked still counts.
    /// </summary>
    public sealed class ShopPurchasesCondition : ICondition
    {
        private readonly Func<IShopService> _shop;
        private readonly string[] _storefrontIds;
        private readonly int _min;
        private readonly string _reasonKey;

        public ShopPurchasesCondition(Func<IShopService> shop, string[] storefrontIds, int min)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _storefrontIds = storefrontIds ?? Array.Empty<string>();
            _min = min < 1 ? 1 : min;
            _reasonKey = $"shopPurchases.{string.Join("+", _storefrontIds)}";
        }

        public ConditionResult Evaluate()
        {
            var shop = _shop();
            if (shop == null) return ConditionResult.Leaf(0, _min, _reasonKey);

            var total = 0L;
            for (var i = 0; i < _storefrontIds.Length; i++)
            {
                var lots = shop.GetLots(_storefrontIds[i]);
                if (lots == null) continue;

                for (var j = 0; j < lots.Count; j++)
                {
                    var lot = lots[j];
                    if (lot == null || string.IsNullOrEmpty(lot.LotId)) continue;
                    total += shop.GetPurchaseCount(lot.LotId);
                }
            }

            return ConditionResult.Leaf(total, _min, _reasonKey);
        }
    }
}
