using Game.Quest.API;

namespace Game.Characters.UI
{
    /// <summary>Immutable row data for one memory in the Journal Characters view.</summary>
    public sealed class JournalMemoryItemModel
    {
        public JournalMemoryItemModel(string memoryId, string titleKey, string descriptionKey,
            bool isUnlocked, bool isGolden, QuestState linkedQuestState)
        {
            MemoryId = memoryId;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            IsUnlocked = isUnlocked;
            IsGolden = isGolden;
            LinkedQuestState = linkedQuestState;
        }

        public string MemoryId { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public bool IsUnlocked { get; }
        public bool IsGolden { get; }
        public QuestState LinkedQuestState { get; }
    }
}
