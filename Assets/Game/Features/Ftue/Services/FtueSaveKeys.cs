namespace Game.Ftue.Services
{
    /// <summary>
    /// Ключи Save-модулей FTUE-фазы. По образцу <c>Book.Sell.API.SalesSaveKeys</c>
    /// и <c>Game.Preparation.Services.PreparationSaveKeys</c>.
    /// </summary>
    public static class FtueSaveKeys
    {
        public const string Applied = "ftue.applied";
        public const int AppliedSchemaVersion = 1;

        // Welcome letter window only. Set true after the player clicks Start. Quit before Start ->
        // stays false -> the window re-shows on next entry. NOT a "first entry complete" flag.
        public const string WelcomeCompleted = "ftue.welcome_completed";
        public const int WelcomeCompletedSchemaVersion = 1;

        // First-location tutorial — own lifecycle (NotStarted/InProgress/Completed), Completed set
        // only at the end of the tutorial. Scaffolded now; tutorial logic is a future task. Kept
        // separate from WelcomeCompleted so resuming mid-location is possible without skipping it.
        public const string FirstLocationTutorial = "ftue.first_location_tutorial";
        public const int FirstLocationTutorialSchemaVersion = 1;
    }
}
