namespace Game.Quest.Services.Persistence
{
    /// <summary>Save module keys owned by the Quest feature.</summary>
    public static class QuestsSaveKeys
    {
        public const string State = "quests";

        // Release baseline: all pre-release saves are wiped, so the public schema starts at v1.
        public const int StateSchemaVersion = 1;
    }
}
