using System.Collections.Generic;

namespace Game.Decor
{
    /// <summary>
    /// Save module payload: which decor sits in which slot at the player's current location.
    /// Schema 1. Loaded via ISaveHook.AfterLoadAsync, persisted on every mutation.
    /// </summary>
    public sealed class DecorPlacementState
    {
        public int Schema { get; set; } = 1;
        public List<DecorPlacementEntry> Placements { get; set; } = new();

        /// <summary>True once the player has claimed the Day 1 free decor (vintage_globe).</summary>
        public bool FirstDayRewardClaimed { get; set; }

        /// <summary>True once the player has bought the Day 1 paid offer (coffee_pot).</summary>
        public bool FirstDayPurchaseDone { get; set; }
    }
}
