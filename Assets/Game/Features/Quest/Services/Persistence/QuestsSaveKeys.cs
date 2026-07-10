namespace Game.Quest.Services.Persistence
{
    /// <summary>Save module keys owned by the Quest feature.</summary>
    public static class QuestsSaveKeys
    {
        public const string State = "quests";

        // v4 stores readable task DTOs with string enum states and inline compact sales baselines.
        public const int StateSchemaVersion = 4;
    }
}
