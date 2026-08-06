namespace Game.Journal.UI
{
    /// <summary>
    /// Titles for the Journal tab header. Plain strings for now; each value becomes a localization
    /// id once localization lands, so this is the only place that has to change.
    /// </summary>
    public static class JournalTabTitles
    {
        public const string Memories = "Memories";
        public const string Places = "Locations";
        public const string Objects = "Decorations";
        public const string People = "Characters";
        public const string Quests = "Quests";

        public static string Get(JournalTab tab) => tab switch
        {
            JournalTab.Memories => Memories,
            JournalTab.Places => Places,
            JournalTab.Objects => Objects,
            JournalTab.People => People,
            JournalTab.Quests => Quests,
            _ => string.Empty
        };
    }
}
