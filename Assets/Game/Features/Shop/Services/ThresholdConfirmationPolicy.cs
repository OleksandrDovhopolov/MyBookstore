using Game.Shop.API;

namespace Game.Shop.Services
{
    /// <summary>
    /// Default <see cref="IShopConfirmationPolicy"/>: confirm when <c>lot.Price.Amount &gt; Threshold</c>.
    /// Threshold is hardcoded at 50 gold for Phase 1 — matches the current paid decor lot
    /// (<c>newspaper_decor_coffee_pot</c>, 50g) so that lot does NOT trigger confirm, while anything
    /// above does. Classic Shop (PR11) ассортимент сделает порог осмысленным.
    /// </summary>
    public sealed class ThresholdConfirmationPolicy : IShopConfirmationPolicy
    {
        private const int Threshold = 50;

        public bool RequiresConfirmation(ShopLot lot)
        {
            if (lot == null) return false;
            return lot.Price.Amount > Threshold;
        }
    }
}
