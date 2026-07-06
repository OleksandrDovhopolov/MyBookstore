namespace Game.Quest.Services.Persistence
{
    /// <summary>Save module keys owned by the Quest feature.</summary>
    public static class QuestsSaveKeys
    {
        public const string State = "quests";

        // v3 replaces full per-task SalesStatsStateDto baselines with compact SalesStatsBaselineDto.
        public const int StateSchemaVersion = 3;
    }
}
