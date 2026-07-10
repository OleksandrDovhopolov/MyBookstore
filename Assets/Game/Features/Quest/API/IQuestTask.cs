using Game.Conditions.API;
using Game.Configs.Models;

namespace Game.Quest.API
{
    /// <summary>
    /// A single task within a quest. Read surface for UI/consumers; state transitions are owned by the
    /// service implementation. Progress is exposed as a <see cref="ConditionResult"/> so UI can render it
    /// the same way as location-unlock progress ("Crime 3/10", composite trees).
    /// </summary>
    public interface IQuestTask
    {
        int Id { get; }
        string QuestId { get; }
        QuestTaskState State { get; }
        QuestTaskConfig Config { get; }

        /// <summary>Current evaluation of the task's completion conditions (numerator/denominator/children).</summary>
        ConditionResult Progress { get; }

        /// <summary>Convenience over <see cref="Progress"/>: current count toward the goal.</summary>
        int GetProgress();

        /// <summary>Convenience over <see cref="Progress"/>: target count (always &gt;= 1).</summary>
        int GetGoal();
    }
}
