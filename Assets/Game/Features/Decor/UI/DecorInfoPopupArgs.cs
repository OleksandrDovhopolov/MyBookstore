using Game.UI;

namespace Game.Decor.UI
{
    /// <summary>Arguments for <see cref="DecorInfoPopup"/>. Opens additively (over the placement window).</summary>
    public sealed class DecorInfoPopupArgs : WindowArgs
    {
        public string DecorId { get; }

        public DecorInfoPopupArgs(string decorId)
        {
            DecorId = decorId;
            AsAdditional(); // stack over DecorPlacementWindow without closing it
        }
    }
}
