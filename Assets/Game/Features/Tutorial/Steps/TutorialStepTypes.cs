namespace Game.Tutorial.Steps
{
    /// <summary>Canonical step-type discriminators (match tutorials.json "type" values).</summary>
    public static class TutorialStepTypes
    {
        public const string ShowText = "showText";
        public const string HighlightClick = "highlightClick";
        public const string AwaitWindow = "awaitWindow";
        public const string AwaitQuest = "awaitQuest";
        public const string AwaitPhase = "awaitPhase";
        public const string AwaitLocation = "awaitLocation";
    }
}
