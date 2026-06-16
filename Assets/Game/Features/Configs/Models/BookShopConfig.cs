namespace Game.Configs.Models
{
    /// <summary>
    /// Player's bookshop entity (cart/stand). Owns decor slots — they travel with the shop
    /// across Locations. Independent of <see cref="LocationConfig"/>: a Location is *where* the
    /// shop stands today (demand, unlock cost), not *what slots* it has.
    /// Файл: bookshops.json (JSON-массив).
    /// </summary>
    [ConfigFile("bookshops")]
    public sealed class BookShopConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }

        /// <summary>Decor placement slots on this bookshop. Slot accepts a decor only when both
        /// PositionType and MaxSize are compatible.</summary>
        public DecorSlot[] DecorSlots { get; set; }
    }
}
