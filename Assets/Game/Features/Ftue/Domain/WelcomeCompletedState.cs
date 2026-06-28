namespace Game.Ftue.Domain
{
    /// <summary>
    /// Tracks ONLY the welcome letter window: set <see cref="Completed"/> = true after the player
    /// clicks Start. If the player quits before Start, it stays false and the window re-shows on the
    /// next entry. This is deliberately NOT a "first entry finished" flag — the first-location
    /// tutorial has its own <see cref="FirstLocationTutorialState"/>.
    /// POCO: serialized by Newtonsoft via ISaveService under <c>FtueSaveKeys.WelcomeCompleted</c>.
    /// </summary>
    public sealed class WelcomeCompletedState
    {
        public bool Completed { get; set; }

        /// <summary>ISO-8601 UTC timestamp of when Start was clicked. Audit-only.</summary>
        public string CompletedAtUtcIso { get; set; }
    }
}
