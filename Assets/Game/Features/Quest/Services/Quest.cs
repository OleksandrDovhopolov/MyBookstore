using System.Collections.Generic;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.Quest.API;

namespace Game.Quest.Services
{
    /// <summary>
    /// Runtime quest instance. State transitions are driven by <see cref="QuestsService"/>.
    /// </summary>
    internal sealed class Quest : IQuest
    {
        private readonly ICondition _activation;   // null/empty config → always-met (head auto-activates)
        private readonly ICondition _fail;         // null → never fails
        private readonly List<QuestTask> _tasks;

        public string Id { get; }
        public QuestType Type { get; }
        public QuestState State { get; private set; } = QuestState.Pending;
        public string ChainId { get; }
        public string CharacterId { get; }
        public QuestConfig Config { get; }

        /// <summary>0 or 1 element (MVP linear chain).</summary>
        public IReadOnlyList<string> NextQuestIds { get; }

        public IReadOnlyList<IQuestTask> Tasks => _tasks;
        internal IReadOnlyList<QuestTask> TasksInternal => _tasks;

        public Quest(
            QuestConfig config,
            QuestType type,
            ICondition activation,
            ICondition fail,
            List<QuestTask> tasks,
            IReadOnlyList<string> nextQuestIds)
        {
            Config = config;
            Id = config.Id;
            Type = type;
            ChainId = config.ChainId;
            CharacterId = config.CharacterId;
            _activation = activation;
            _fail = fail;
            _tasks = tasks;
            NextQuestIds = nextQuestIds;
        }

        public IQuestTask GetTask(int id)
        {
            for (var i = 0; i < _tasks.Count; i++)
                if (_tasks[i].Id == id) return _tasks[i];
            return null;
        }

        internal bool IsActivationMet() => _activation.Evaluate().IsMet;
        internal bool IsFailMet() => _fail != null && _fail.Evaluate().IsMet;
        internal void SetState(QuestState state) => State = state;
    }
}
