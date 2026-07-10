namespace Game.Tutorial.Services
{
    /// <summary>Canonical trigger discriminators (match tutorials.json "trigger" values).</summary>
    public static class TutorialTriggers
    {
        public const string HubReady = "hubReady";
        public const string LocationLoaded = "locationLoaded";
        public const string PhaseChanged = "phaseChanged";
        public const string QuestStarted = "questStarted";
        public const string QuestCompleted = "questCompleted";
    }
}
