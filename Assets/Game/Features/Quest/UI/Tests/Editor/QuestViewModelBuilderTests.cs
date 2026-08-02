using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Quest.API;
using Game.Rewards.API;
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
            public int GetGoal() => (int)Math.Max(1, Progress.Target);
        }

        private sealed class FakeQuest : IQuest
        {
            public string Id { get; set; }
            public QuestType Type { get; set; }
            public QuestState State { get; set; }
            public string ChainId { get; set; }
            public QuestConfig Config { get; set; }
            public IReadOnlyList<IQuestTask> Tasks { get; set; } = Array.Empty<IQuestTask>();
            public IQuestTask GetTask(int id) => Tasks?.FirstOrDefault(t => t.Id == id);
        }

        [Test]
        public void Build_HidesPending_AndMapsCardFields()
        {
            var configs = new FakeConfigsService()
                .Add(new CharacterConfig { Id = "eddi", PortraitKey = "eddi" });
            var pending = Quest("pending", QuestState.Pending, "milly");
            var active = Quest("q_intro_eddi", QuestState.Active, "eddi",
                tasks: new IQuestTask[]
                {
                    Task(1, "task.one", QuestTaskState.Active, ConditionResult.Leaf(2, 3, "one"))
                });

            var models = new QuestViewModelBuilder(configs).Build(new[] { pending, active });

            Assert.AreEqual(1, models.Count);
            var m = models[0];
            Assert.AreEqual("q_intro_eddi", m.Id);
            Assert.AreEqual("eddi", m.CharacterId);
            Assert.AreEqual("eddi", m.CharacterPortraitKey);
            Assert.AreEqual("quest.q_intro_eddi.title", m.TitleKey);
            Assert.AreEqual("quest.q_intro_eddi.desc", m.DescriptionKey);
            Assert.AreEqual(QuestState.Active, m.State);
            Assert.IsFalse(m.CanClaim);
            Assert.IsFalse(m.IsRewardClaimed);
            Assert.IsFalse(m.IsFailed);
        }

        [Test]
        public void Build_MapsAllTasks_InOrder_AndUnwrapsSingleChildComposite()
        {
            var quest = Quest("q1", QuestState.Active, "eddi",
                tasks: new IQuestTask[]
                {
                    Task(1, "task.first", QuestTaskState.Completed, ConditionResult.Leaf(5, 5, "first")),
                    Task(2, "task.second", QuestTaskState.Active,
                        new ConditionResult(false, 0, 1, "all", new[] { ConditionResult.Leaf(2, 4, "second") }))
                });

            var tasks = new QuestViewModelBuilder().Build(new[] { quest })[0].Tasks;

            Assert.AreEqual(2, tasks.Count);
            Assert.AreEqual("task.first", tasks[0].DescriptionKey);
            Assert.AreEqual(5, tasks[0].Current);
            Assert.AreEqual(5, tasks[0].Goal);
            Assert.IsTrue(tasks[0].IsDone);
            Assert.AreEqual("task.second", tasks[1].DescriptionKey);
            Assert.AreEqual(2, tasks[1].Current);
            Assert.AreEqual(4, tasks[1].Goal);
            Assert.AreEqual(0.5f, tasks[1].Fill01);
        }

        [Test]
        public void Build_CompletedQuest_ClampsTasksToFullProgress()
        {
            var quest = Quest("q1", QuestState.ReadyToAward, "eddi",
                tasks: new IQuestTask[]
                {
                    Task(1, "task", QuestTaskState.Active, ConditionResult.Leaf(0, 3, "task"))
                });

            var model = new QuestViewModelBuilder().Build(new[] { quest })[0];

            Assert.IsTrue(model.IsComplete);
            Assert.IsTrue(model.CanClaim);
            Assert.AreEqual(3, model.Tasks[0].Current);
            Assert.AreEqual(3, model.Tasks[0].Goal);
            Assert.AreEqual(1f, model.Tasks[0].Fill01);
        }

        [Test]
        public void Build_MapsRewards_WithDisplayNamesAndFallbacks()
        {
            var configs = new FakeConfigsService()
                .Add(new ConsumableConfig { Id = "fuel_canister", DisplayName = "Fuel" })
                .Add(new QuestItemConfig { Id = "milly_letter", DisplayName = "Milly Letter" })
                .Add(new DecorConfig { Id = "lavender", DisplayName = "Lavender" });
            var quest = Quest("q1", QuestState.Awarded, "milly",
                rewards: new[]
                {
                    Reward("fuel_canister", InventoryCategories.Consumable, 2),
                    Reward("milly_letter", InventoryCategories.QuestItem, 1),
                    Reward("lavender", InventoryCategories.Decor, 1),
                    Reward("missing", InventoryCategories.QuestItem, 1)
                });

            var model = new QuestViewModelBuilder(configs).Build(new[] { quest })[0];

            Assert.IsTrue(model.IsRewardClaimed);
            CollectionAssert.AreEqual(new[] { "Fuel", "Milly Letter", "Lavender", "missing" },
                model.Rewards.Select(r => r.DisplayName).ToArray());
            Assert.IsTrue(model.Rewards.All(r => r.Kind == RewardKind.InventoryItem));
        }

        [Test]
        public void Build_FailedQuest_SetsFailedFlag()
        {
            var model = new QuestViewModelBuilder().Build(new[] { Quest("q1", QuestState.Failed, "eddi") })[0];

            Assert.IsTrue(model.IsFailed);
            Assert.IsFalse(model.IsComplete);
            Assert.IsFalse(model.CanClaim);
        }

        [Test]
        public void Build_NullInput_IsEmpty()
        {
            Assert.AreEqual(0, new QuestViewModelBuilder().Build(null).Count);
        }

        private static FakeQuest Quest(
            string id,
            QuestState state,
            string characterId,
            IReadOnlyList<IQuestTask> tasks = null,
            QuestRewardConfig[] rewards = null)
            => new()
            {
                Id = id,
                Type = QuestType.Story,
                State = state,
                Config = new QuestConfig
                {
                    Id = id,
                    CharacterId = characterId,
                    TitleKey = $"quest.{id}.title",
                    DescriptionKey = $"quest.{id}.desc",
                    Rewards = rewards ?? Array.Empty<QuestRewardConfig>()
                },
                Tasks = tasks ?? Array.Empty<IQuestTask>()
            };

        private static FakeTask Task(int id, string descriptionKey, QuestTaskState state, ConditionResult progress)
            => new()
            {
                Id = id,
                State = state,
                Config = new QuestTaskConfig { Id = id, DescriptionKey = descriptionKey },
                Progress = progress
            };

        private static QuestRewardConfig Reward(string id, string category, int amount)
            => new() { Kind = nameof(RewardKind.InventoryItem), Id = id, Category = category, Amount = amount };

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<string, IConfig> _configs = new(StringComparer.Ordinal);

            public FakeConfigsService Add(IConfig config)
            {
                if (config != null && !string.IsNullOrEmpty(config.Id))
                    _configs[config.Id] = config;
                return this;
            }

            public T Get<T>(string id) where T : class, IConfig
            {
                TryGet<T>(id, out var config);
                return config;
            }

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                if (!string.IsNullOrEmpty(id) && _configs.TryGetValue(id, out var stored) && stored is T typed)
                {
                    config = typed;
                    return true;
                }

                config = null;
                return false;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult(Get<T>(id));
            public bool IsExists<T>(string id) where T : class, IConfig => TryGet<T>(id, out _);

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
            {
                var result = new List<T>();
                foreach (var config in _configs.Values)
                    if (config is T typed) result.Add(typed);
                return result;
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
