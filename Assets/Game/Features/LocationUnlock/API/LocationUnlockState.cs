namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Unlock is free and automatic: the moment a location's conditions are satisfied it becomes
    /// <see cref="Unlocked"/> (a persisted fact) — there is no separate "conditions met but not bought"
    /// step anymore. So only two player-facing states remain.
    /// </summary>
    public enum LocationUnlockState
    {
        /// <summary>Conditions not yet satisfied.</summary>
        Locked,

        /// <summary>Conditions satisfied → opened and persisted.</summary>
        Unlocked
    }
}
