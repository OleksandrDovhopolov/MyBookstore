namespace Book.Sell.Domain
{
    /// <summary>
    /// Coarse-grained customer phase for the View / telemetry. Driven by the active step.
    /// </summary>
    public enum CustomerPhase
    {
        Spawned = 0,
        Approaching = 1,
        Browsing = 2,
        AwaitingHelp = 3,   // wants the active minigame but the lock is held by someone else
        InMinigame = 4,     // holds the lock; the player is choosing a book for this customer
        Leaving = 5,
        Done = 6
    }
}
