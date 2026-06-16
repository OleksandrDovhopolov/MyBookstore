using Game.Decor;

namespace Game.Configs.Models
{
    /// <summary>
    /// A placement point on the player's bookshop where a decoration can be placed.
    /// Owned by <see cref="BookShopConfig.DecorSlots"/>. Slot accepts a decor only when both
    /// <see cref="PositionType"/> and <see cref="MaxSize"/> are compatible.
    /// </summary>
    public sealed class DecorSlot
    {
        public string Id { get; set; }
        public DecorPositionType PositionType { get; set; }
        public DecorSize MaxSize { get; set; }
    }
}
