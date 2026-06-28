using Game.Conditions.API;

namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Snapshot of one location's unlock status for UI. <see cref="Progress"/> is the root
    /// <see cref="ConditionResult"/> — walk <see cref="ConditionResult.Children"/> to render
    /// per-requirement progress ("Crime 23/30, Kids 4/5"). Unlocking is free and automatic, so there
    /// is no cost here: the location is either still <see cref="LocationUnlockState.Locked"/> (with
    /// progress) or <see cref="LocationUnlockState.Unlocked"/>.
    /// </summary>
    public sealed class LocationUnlockStatus
    {
        public string LocationId { get; }
        public LocationUnlockState State { get; }
        public ConditionResult Progress { get; }

        public LocationUnlockStatus(string locationId, LocationUnlockState state, ConditionResult progress)
        {
            LocationId = locationId;
            State = state;
            Progress = progress;
        }
    }
}
