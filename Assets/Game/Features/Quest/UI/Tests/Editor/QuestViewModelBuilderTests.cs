using System.Collections.Generic;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.Quest.API;
using NUnit.Framework;

namespace Game.Quest.UI.Tests.Editor
{
    public sealed class QuestViewModelBuilderTests
    {
        private sealed class FakeTask : IQuestTask
        {
            public int Id { get; set; }
            public string QuestId { get; set; }
            public QuestTaskState State { get; set; }
            public QuestTaskConfig Config { get; set; }
            public ConditionResult Progress { get; set; }
            public int GetProgress() => (int)Progress.Current;
            public int GetGoal() => (int)System.Math.Max(1, Progress.Target);
        }

        private sealed class FakeQuest : IQuest
        {
            public string Id { get; set; }
            public QuestType Type { get; set; }
            public QuestState State { get; set; }
            public string ChainId { get; set; }
            public string CharacterId { get; set; }
            public QuestConfig Config { get; set; }
            public IReadOnlyList<IQuestTask> Tasks { get; set; }
            public IQuestTask GetTask(int id) => null;
        }

        [Test]
        public void Build_MapsTitleDescStateAndPrimaryTaskProgress()
        {
            var quest = new FakeQuest
            {
                Id = "far_beach_intro",
                Type = QuestType.Story,
                State = QuestState.Active,
                Config = new QuestConfig
                {
                    TitleKey = "quest.t",
                    DescriptionKey = "quest.d",
                    NextQuestIds = new[] { "sand_inspiration" }
                },
                Tasks = new IQuestTask[]
                {
                    new FakeTask
                    {
                        State = QuestTaskState.Active,
                        Config = new QuestTaskConfig { DescriptionKey = "task.visit_far_beach" },
                        // Real completion is wrapped in {"all":[leaf]}; the builder must unwrap to the leaf (2/3).
                        Progress = new ConditionResult(false, 0, 1, "all", new[] { ConditionResult.Leaf(2, 3, "visit") })
                    }
                }
            };

            var models = new QuestViewModelBuilder().Build(new[] { quest });

            Assert.AreEqual(1, models.Count);
            var m = models[0];
            Assert.AreEqual("far_beach_intro", m.Id);
            Assert.AreEqual("quest.t", m.TitleKey);
            Assert.AreEqual("quest.d", m.DescriptionKey);
            Assert.AreEqual("sand_inspiration", m.NextQuestId);
            Assert.AreEqual("task.visit_far_beach", m.PrimaryTaskKey);
            Assert.AreEqual(2, m.ProgressCurrent);
            Assert.AreEqual(3, m.ProgressGoal);
            Assert.AreEqual(QuestState.Active, m.State);
            Assert.IsFalse(m.IsComplete);
        }

        [Test]
        public void Build_PrimaryTask_IsFirstUncompleted()
        {
            var quest = new FakeQuest
            {
                State = QuestState.Active,
                Config = new QuestConfig { TitleKey = "t" },
                Tasks = new IQuestTask[]
                {
                    new FakeTask { State = QuestTaskState.Completed, Config = new QuestTaskConfig { DescriptionKey = "done" }, Progress = ConditionResult.Leaf(5, 5, "x") },
                    new FakeTask { State = QuestTaskState.Active, Config = new QuestTaskConfig { DescriptionKey = "active" }, Progress = ConditionResult.Leaf(1, 4, "x") }
                }
            };

            var m = new QuestViewModelBuilder().Build(new[] { quest })[0];
            Assert.AreEqual("active", m.PrimaryTaskKey);
            Assert.AreEqual(1, m.ProgressCurrent);
            Assert.AreEqual(4, m.ProgressGoal);
        }

        [Test]
        public void Build_IsComplete_WhenReadyToAward_NoTasksGoalDefaultsOne()
        {
            var quest = new FakeQuest
            {
                State = QuestState.ReadyToAward,
                Config = new QuestConfig { TitleKey = "t" },
                Tasks = new IQuestTask[0]
            };

            var m = new QuestViewModelBuilder().Build(new[] { quest })[0];
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual(0, m.ProgressCurrent);
            Assert.AreEqual(1, m.ProgressGoal);
            Assert.IsNull(m.PrimaryTaskKey);
        }

        [Test]
        public void Build_NullInput_IsEmpty()
        {
            Assert.AreEqual(0, new QuestViewModelBuilder().Build(null).Count);
        }
    }
}
