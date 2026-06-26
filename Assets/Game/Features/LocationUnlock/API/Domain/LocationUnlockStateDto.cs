using System.Collections.Generic;

namespace Game.LocationUnlock.API
{
    /// <summary>Transport DTO: ids of locations the player has unlocked (purchased).</summary>
    public sealed class LocationUnlockStateDto
    {
        public List<string> UnlockedIds { get; set; }
    }
}
