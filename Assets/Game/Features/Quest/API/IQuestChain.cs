using System.Collections.Generic;

namespace Game.Quest.API
{
    /// <summary>
    /// An ordered sequence of quests linked by <c>QuestConfig.NextQuestIds</c>. Linear for the MVP
    /// (each quest has 0 or 1 successor).
    /// </summary>
    public interface IQuestChain
    {
        string Id { get; }

        IReadOnlyList<IQuest> Quests { get; }

        /// <summary>First active quest, else first pending, else the final one (see docs/QUESTS.md §10).</summary>
        IQuest CurrentQuest { get; }

        /// <summary>Last quest in the chain.</summary>
        IQuest FinalQuest { get; }
    }
}
