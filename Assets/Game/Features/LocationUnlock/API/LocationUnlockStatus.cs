using Game.Conditions.API;

namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Snapshot of one location's condition progress for UI. Item costs are exposed separately through
    /// <see cref="ILocationUnlockService.GetCost"/>.
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
