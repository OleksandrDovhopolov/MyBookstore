namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Three distinct states — kept separate because a location can satisfy its conditions yet still
    /// not be purchased (<see cref="LocationConfig.UnlockCost"/>). "Conditions met" (computed live) is
    /// NOT "unlocked" (a persisted purchase fact).
    /// </summary>
    public enum LocationUnlockState
    {
        /// <summary>Conditions not yet satisfied.</summary>
        Locked,

        /// <summary>Conditions satisfied, not yet purchased — <c>TryUnlockAsync</c> will spend the cost.</summary>
        Unlockable,

        /// <summary>Purchased/opened — persisted.</summary>
        Unlocked
    }
}
