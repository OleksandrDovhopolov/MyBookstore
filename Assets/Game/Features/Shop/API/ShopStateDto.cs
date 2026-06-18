using System.Collections.Generic;

namespace Game.Shop.API
{
    /// <summary>
    /// Transport DTO between <see cref="IShopService"/> and the save backend. Lives in the API
    /// assembly so a future server-backed implementation can target it without referencing the
    /// concrete service. Forward-compat: unknown lot ids that arrive from a save under a newer
    /// config are preserved as-is (not dropped) so a config downgrade doesn't lose purchase history.
    /// </summary>
    public sealed class ShopStateDto
    {
        public Dictionary<string, LotPurchasesDto> Lots { get; set; } = new();
    }
}
