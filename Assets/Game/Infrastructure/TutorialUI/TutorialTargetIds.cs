namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Canonical tutorial highlight-target ids (match the "target" field in tutorials.json). Kept here so
    /// controllers that register rects and the step handlers that resolve them share one vocabulary.
    /// </summary>
    public static class TutorialTargetIds
    {
        public const string HubStartDayButton = "hub.start_day_button";
        public const string HubDecorButton = "hub.decor_button";
    }
}
