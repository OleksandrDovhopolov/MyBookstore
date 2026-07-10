using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.Quest.API
{
    /// <summary>
    /// A single quest instance. Read surface for UI/consumers; lifecycle transitions are driven by
    /// <see cref="IQuestsService"/>.
    /// </summary>
    public interface IQuest
    {
        string Id { get; }
        QuestType Type { get; }
        QuestState State { get; }

        string ChainId { get; }

        /// <summary>Owning character; null until the characters feature exists.</summary>
        string CharacterId { get; }

        QuestConfig Config { get; }

        IReadOnlyList<IQuestTask> Tasks { get; }

        /// <summary>Task by its numeric id, or null if not found.</summary>
        IQuestTask GetTask(int id);
    }
}
