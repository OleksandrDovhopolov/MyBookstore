namespace Game.Journal.UI
{
    /// <summary>
    /// Attention keys owned by the Journal. Kept in this assembly rather than in
    /// <c>Game.Attention.API</c> so the shared core stays ignorant of the features using it.
    /// <para>
    /// Quests needs <b>two</b> keys: the badge must distinguish "a quest just became active" from
    /// "a quest is waiting to be awarded", and each has its own seen-set.
    /// </para>
    /// </summary>
    public static class JournalAttentionKeys
    {
        public const string QuestsNew = "journal.quests.new";
        public const string QuestsAward = "journal.quests.award";
        public const string Places = "journal.places";
        public const string People = "journal.people";
        public const string Memories = "journal.memories";

        /// <summary>Every Journal key — used for the HUD journal badge. Static so refreshes do not allocate.</summary>
        public static readonly string[] All = { QuestsNew, QuestsAward, Places, People, Memories };

        /// <summary>The Quests tab maps to both quest keys.</summary>
        public static readonly string[] QuestsTab = { QuestsNew, QuestsAward };

        public static readonly string[] PlacesTab = { Places };
        public static readonly string[] PeopleTab = { People };
        public static readonly string[] MemoriesTab = { Memories };
    }
}
