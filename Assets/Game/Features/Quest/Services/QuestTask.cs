using System;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.Quest.API;

namespace Game.Quest.Services
{
    /// <summary>
    /// Runtime task instance. State transitions are driven by <see cref="QuestsService"/>; this type holds
    /// the parsed condition trees and the cached <see cref="ConditionResult"/> progress.
    /// </summary>
    internal sealed class QuestTask : IQuestTask
    {
        private readonly ICondition _activation;   // null/empty config → always-met (parser contract)
        private readonly ICondition _completion;   // null/empty config → always-met (task completes at once)

        public int Id { get; }
        public string QuestId { get; }
        public QuestTaskConfig Config { get; }
        public QuestTaskState State { get; private set; } = QuestTaskState.Pending;
        public ConditionResult Progress { get; private set; }

        public QuestTask(string questId, QuestTaskConfig config, ICondition activation, ICondition completion)
        {
            QuestId = questId;
            Id = config.Id;
            Config = config;
            _activation = activation;
            _completion = completion;
            Progress = _completion.Evaluate();
        }

        public int GetProgress() => (int)Math.Max(0, Progress.Current);
        public int GetGoal() => (int)Math.Max(1, Progress.Target);

        internal bool IsActivationMet() => _activation.Evaluate().IsMet;

        /// <summary>Re-evaluates completion; returns true if the displayed progress tuple changed.</summary>
        internal bool RefreshProgress()
        {
            var next = _completion.Evaluate();
            var changed = next.IsMet != Progress.IsMet
                          || next.Current != Progress.Current
                          || next.Target != Progress.Target
                          || next.ReasonKey != Progress.ReasonKey;
            Progress = next;
            return changed;
        }

        internal bool IsCompletionMet => Progress.IsMet;

        internal void SetState(QuestTaskState state) => State = state;
    }
}
