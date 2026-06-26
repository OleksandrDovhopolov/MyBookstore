using Game.Conditions.API;

namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Snapshot of one location's unlock status for UI. <see cref="Progress"/> is the root
    /// <see cref="ConditionResult"/> — walk <see cref="ConditionResult.Children"/> to render
    /// per-requirement progress ("Crime 23/30, Kids 4/5").
    /// </summary>
    public sealed class LocationUnlockStatus
    {
        public string LocationId { get; }
        public LocationUnlockState State { get; }
        public int UnlockCost { get; }
        public ConditionResult Progress { get; }

        public LocationUnlockStatus(string locationId, LocationUnlockState state, int unlockCost, ConditionResult progress)
        {
            LocationId = locationId;
            State = state;
            UnlockCost = unlockCost;
            Progress = progress;
        }
    }
}
