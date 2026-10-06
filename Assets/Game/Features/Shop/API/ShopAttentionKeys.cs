namespace Game.Shop.API
{
    /// <summary>
    /// Attention keys owned by the Shop. Lives here rather than in <c>Game.Attention.API</c> so the
    /// shared attention core stays ignorant of the features using it, and so both the HUD (which
    /// shows the badge) and the shop window (which clears it) can name the key.
    /// </summary>
    public static class ShopAttentionKeys
    {
        /// <summary>New decor lots became purchasable — drives the badge on the HUD shop button.</summary>
        public const string Decor = "shop.decor";
    }
}
