using Game.Decor;

namespace Game.Configs.Models
{
    /// <summary>
    /// A placement point in a location where the player can put a decoration.
    /// Owned by <see cref="LocationConfig.DecorSlots"/>. Slot accepts a decor only when both
    /// <see cref="PositionType"/> and <see cref="MaxSize"/> are compatible.
    /// </summary>
    public sealed class DecorSlot
    {
        public string Id { get; set; }
        public DecorPositionType PositionType { get; set; }
        public DecorSize MaxSize { get; set; }
    }
}
