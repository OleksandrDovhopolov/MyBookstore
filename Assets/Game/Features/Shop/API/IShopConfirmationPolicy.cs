namespace Game.Shop.API
{
    /// <summary>
    /// Decides whether a given <see cref="ShopLot"/> requires an explicit confirmation step before
    /// the purchase pipeline runs. The UI layer is responsible for actually displaying the dialog —
    /// this interface only encodes the policy.
    /// </summary>
    /// <remarks>
    /// Default impl <c>ThresholdConfirmationPolicy</c> compares <c>lot.Price.Amount</c> against a
    /// fixed threshold. Phase 2+ may swap in a config-driven policy (per-lot flag in
    /// <c>ShopConfig</c>) or a per-storefront policy without UI changes.
    /// </remarks>
    public interface IShopConfirmationPolicy
    {
        bool RequiresConfirmation(ShopLot lot);
    }
}
