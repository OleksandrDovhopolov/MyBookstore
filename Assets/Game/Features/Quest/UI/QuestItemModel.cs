using Game.Quest.API;

namespace Game.Quest.UI
{
    /// <summary>
    /// Display model for one quest row. Text fields are raw localization keys until INF-4 localization
    /// exists (resolved in one place then). Built by <see cref="QuestViewModelBuilder"/>.
    /// </summary>
    public sealed class QuestItemModel
    {
        public string Id;
        public string TitleKey;
        public string DescriptionKey;
        public QuestState State;
        public QuestType Type;

        /// <summary>Next quest id in the chain (first of QuestConfig.NextQuestIds), or null if this is the last.</summary>
        public string NextQuestId;

        /// <summary>Description key of the primary (currently-worked) task.</summary>
        public string PrimaryTaskKey;
        public int ProgressCurrent;
        public int ProgressGoal;

        /// <summary>Quest reached ReadyToAward/Awarded.</summary>
        public bool IsComplete;
    }
}
