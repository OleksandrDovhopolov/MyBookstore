namespace Game.Journal.UI
{
    /// <summary>
    /// Localization keys for the Journal tab header.
    /// </summary>
    public static class JournalTabTitles
    {
        public const string Memories = "ui.journal.tab.memories";
        public const string Places = "ui.journal.tab.locations";
        public const string Objects = "ui.journal.tab.objects";
        public const string People = "ui.journal.tab.characters";
        public const string Quests = "ui.journal.tab.quests";

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
