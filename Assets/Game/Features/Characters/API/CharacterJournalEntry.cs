using Game.Quest.API;

namespace Game.Characters.API
{
    /// <summary>
    /// Flat read model for the Journal Characters tab. Assembled by the characters feature so UI never
    /// stitches together CharacterConfig + saved state + quest state itself (docs/CHARACTER_SYSTEM.md §11).
    /// </summary>
    public sealed class CharacterJournalEntry
    {
        public string CharacterId { get; set; }
        public bool Discovered { get; set; }
        public string DisplayNameKey { get; set; }
        public string RoleKey { get; set; }

        /// <summary>Addressables key for the character portrait; empty when the UI should show a placeholder.</summary>
        public string PortraitKey { get; set; }

        public CharacterJournalMemory[] Memories { get; set; }
    }

    /// <summary>One memory row in a <see cref="CharacterJournalEntry"/>.</summary>
    public sealed class CharacterJournalMemory
    {
        public string MemoryId { get; set; }
        public bool Unlocked { get; set; }
        public bool IsGolden { get; set; }
        public string TitleKey { get; set; }
        public string DescriptionKey { get; set; }
        public string PhotoKey { get; set; }
        public string LinkedQuestId { get; set; }
        public QuestState LinkedQuestState { get; set; }
    }
}
