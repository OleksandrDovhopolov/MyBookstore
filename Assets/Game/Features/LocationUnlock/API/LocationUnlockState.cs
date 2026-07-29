namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Player-facing open state. Conditions and optional item costs are exposed separately as progress.
    /// </summary>
    public enum LocationUnlockState
    {
        /// <summary>Location is not opened yet.</summary>
        Locked,

        /// <summary>Location is opened and persisted.</summary>
        Unlocked
    }
}
