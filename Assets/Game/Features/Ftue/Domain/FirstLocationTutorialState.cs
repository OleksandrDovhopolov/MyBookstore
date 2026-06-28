namespace Game.Ftue.Domain
{
    /// <summary>Lifecycle of the scripted first-location tutorial.</summary>
    public enum FirstLocationTutorialStage
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2
    }

    /// <summary>
    /// Scaffolding for the future first-location tutorial task. Its completion point differs from the
    /// welcome window (see <see cref="WelcomeCompletedState"/>): <see cref="FirstLocationTutorialStage.Completed"/>
    /// must be set only at the END of the tutorial, so a player who quits mid-location resumes the
    /// tutorial instead of skipping it. Defined now to reserve the save contract; no tutorial logic
    /// is wired yet.
    /// POCO: serialized by Newtonsoft via ISaveService under <c>FtueSaveKeys.FirstLocationTutorial</c>.
    /// </summary>
    public sealed class FirstLocationTutorialState
    {
        public FirstLocationTutorialStage Stage { get; set; }

        /// <summary>ISO-8601 UTC timestamp of the last stage change. Audit-only.</summary>
        public string UpdatedAtUtcIso { get; set; }
    }
}
