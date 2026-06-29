namespace Game.Quest.Services.Persistence
{
    /// <summary>Save module keys owned by the Quest feature.</summary>
    public static class QuestsSaveKeys
    {
        public const string State = "quests";

        // v2 adds per-task sales baseline (SavedQuest.TaskBaseline) for "since activation" progress.
        public const int StateSchemaVersion = 2;
    }
}
