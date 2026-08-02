using Game.Quest.API;

namespace Game.Journal.UI
{
    /// <summary>Immutable row data for one memory in the Journal Characters view.</summary>
    public sealed class JournalMemoryItemModel
    {
        public JournalMemoryItemModel(string characterId, string memoryId, string titleKey, string descriptionKey,
            string photoKey, int order, bool isUnlocked, bool isGolden, QuestState linkedQuestState)
        {
            CharacterId = characterId;
            MemoryId = memoryId;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            PhotoKey = photoKey;
            Order = order;
            IsUnlocked = isUnlocked;
            IsGolden = isGolden;
            LinkedQuestState = linkedQuestState;
        }

        public string CharacterId { get; }
        public string MemoryId { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public string PhotoKey { get; }
        public int Order { get; }
        public bool IsUnlocked { get; }
        public bool IsGolden { get; }
        public QuestState LinkedQuestState { get; }
    }
}
