using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Configs.Models;
using Game.Quest.API;
using Game.Quest.Services;
using Game.Quest.Services.Persistence;
using Game.Quest.Tests.Editor.Fakes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Quest.Tests.Editor
{
    public sealed class QuestsServiceSaveTests
    {
        private static JObject Tag(string tag) => new JObject { ["tag"] = tag };

        private static QuestTaskConfig Task(int id, JObject completion, JObject activation = null)
            => new QuestTaskConfig { Id = id, CompletionConditions = completion, ActivationConditions = activation };

        private static QuestConfig QuestCfg(string id, QuestTaskConfig[] tasks, JObject activation = null,
            JObject fail = null, string[] next = null, string chainId = null)
            => new QuestConfig
            {
                Id = id,
                Type = "story",
                ChainId = chainId,
                NextQuestIds = next,
                Tasks = tasks,
                ActivationConditions = activation,
                FailConditions = fail
            };

        private sealed class Session
        {
            public QuestsService Service;
            public FakeConditionParser Parser;
            public FakeSalesStatsService Sales;
            public readonly List<string> Events = new();

            public void Load() => Service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            public void Save() => Service.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private static Session NewSession(FakeQuestsRepository repo, params QuestConfig[] quests)
        {
            var s = new Session { Parser = new FakeConditionParser(), Sales = new FakeSalesStatsService() };
            var configs = new FakeConfigsService();
            foreach (var q in quests) configs.Add(q);
            s.Service = new QuestsService(new FakeSaveService(), configs, s.Parser, repository: repo, sales: s.Sales);
            s.Service.QuestStarted += q => s.Events.Add($"started:{q.Id}");
            s.Service.QuestAwarded += q => s.Events.Add($"awarded:{q.Id}");
            s.Service.QuestFailed += q => s.Events.Add($"failed:{q.Id}");
            return s;
        }

        [Test]
        public void ReadyToAward_SurvivesRestart_AndCanBeAwardedAfterRestart()
        {
            var repo = new FakeQuestsRepository();
            var cfg = QuestCfg("q1", new[] { Task(1, Tag("c1")) });

            var s1 = NewSession(repo, cfg);
            var c1 = s1.Parser.Register("c1", false);
            s1.Load();
            c1.Met = true;
            s1.Sales.RaiseChanged();
            s1.Save();
            Assert.AreEqual(QuestState.ReadyToAward, repo.Stored.Active["q1"].State);

            var s2 = NewSession(repo, cfg);
            s2.Parser.Register("c1", false);
            s2.Load();

            Assert.AreEqual(QuestState.ReadyToAward, s2.Service.GetQuestState("q1"));
            CollectionAssert.DoesNotContain(s2.Events, "awarded:q1");

            Assert.IsTrue(s2.Service.TryAwardAsync("q1", CancellationToken.None).GetAwaiter().GetResult());
            s2.Save();
            Assert.AreEqual(QuestState.Awarded, s2.Service.GetQuestState("q1"));
            Assert.Contains("q1", repo.Stored.Awarded);
        }

        [Test]
        public void Awarded_SurvivesRestart_AndIsNotReAwarded()
        {
            var repo = new FakeQuestsRepository();
            var cfg = QuestCfg("q1", new[] { Task(1, Tag("c1")) });

            var s1 = NewSession(repo, cfg);
            var c1 = s1.Parser.Register("c1", false);
            s1.Load();
            c1.Met = true;
            s1.Sales.RaiseChanged();
            Assert.IsTrue(s1.Service.TryAwardAsync("q1", CancellationToken.None).GetAwaiter().GetResult());
            s1.Save();
            Assert.Contains("q1", repo.Stored.Awarded);

            var s2 = NewSession(repo, cfg);
            s2.Parser.Register("c1", false);
            s2.Load();

            Assert.AreEqual(QuestState.Awarded, s2.Service.GetQuestState("q1"));
            CollectionAssert.DoesNotContain(s2.Events, "awarded:q1");
        }

        [Test]
        public void Failed_SurvivesRestart_WithoutReFiring()
        {
            var repo = new FakeQuestsRepository();
            var cfg = QuestCfg("q1", new[] { Task(1, Tag("c")) }, fail: Tag("f"));

            var s1 = NewSession(repo, cfg);
            s1.Parser.Register("c", false);
            s1.Parser.Register("f", true);
            s1.Load();
            s1.Save();
            Assert.Contains("q1", repo.Stored.Failed);

            var s2 = NewSession(repo, cfg);
            s2.Parser.Register("c", false);
            s2.Parser.Register("f", true);
            s2.Load();

            Assert.AreEqual(QuestState.Failed, s2.Service.GetQuestState("q1"));
            CollectionAssert.DoesNotContain(s2.Events, "failed:q1");
        }

        [Test]
        public void NonMonotonicTask_StaysCompleted_AfterConditionReverts()
        {
            var repo = new FakeQuestsRepository();
            var cfg = QuestCfg("q1", new[] { Task(1, Tag("c1")), Task(2, Tag("c2")) });

            var s1 = NewSession(repo, cfg);
            s1.Parser.Register("c1", true);
            s1.Parser.Register("c2", false);
            s1.Load();
            s1.Save();
            Assert.AreEqual(QuestState.Active, s1.Service.GetQuestState("q1"));

            var s2 = NewSession(repo, cfg);
            s2.Parser.Register("c1", false);
            s2.Parser.Register("c2", false);
            s2.Load();

            Assert.AreEqual(QuestTaskState.Completed, s2.Service.TryGetQuest("q1").GetTask(1).State,
                "completed task must not roll back after restart");
            Assert.AreEqual(QuestState.Active, s2.Service.GetQuestState("q1"));
        }

        [Test]
        public void PendingQuest_NotPersisted()
        {
            var repo = new FakeQuestsRepository();
            var pending = QuestCfg("pending", new[] { Task(1, Tag("c")) }, activation: Tag("act"));
            var active = QuestCfg("active", new[] { Task(1, Tag("c2")) });

            var s = NewSession(repo, pending, active);
            s.Parser.Register("act", false);
            s.Parser.Register("c", false);
            s.Parser.Register("c2", false);
            s.Load();
            s.Save();

            CollectionAssert.Contains(repo.Stored.Active.Keys, "active");
            CollectionAssert.DoesNotContain(repo.Stored.Active.Keys, "pending");
        }

        [Test]
        public void OfflineProgression_CompletesOnLoad()
        {
            var repo = new FakeQuestsRepository();
            var cfg = QuestCfg("q1", new[] { Task(1, Tag("c1")) });

            var s1 = NewSession(repo, cfg);
            s1.Parser.Register("c1", false);
            s1.Load();
            s1.Save();
            Assert.AreEqual(QuestState.Active, repo.Stored.Active["q1"].State);

            var s2 = NewSession(repo, cfg);
            s2.Parser.Register("c1", true);
            s2.Load();

            Assert.AreEqual(QuestState.ReadyToAward, s2.Service.GetQuestState("q1"));
            Assert.AreEqual(0, s2.Events.Count(e => e == "awarded:q1"));
        }

        [Test]
        public void BeforeSave_BatchedWrite()
        {
            var repo = new FakeQuestsRepository();
            var cfg = QuestCfg("q1", new[] { Task(1, Tag("c1")) });

            var s = NewSession(repo, cfg);
            s.Parser.Register("c1", false);
            s.Load();

            s.Save();
            Assert.AreEqual(1, repo.SaveCallCount);
            s.Save();
            Assert.AreEqual(1, repo.SaveCallCount, "clean state must not re-persist");
        }

        [Test]
        public void BeforeSave_WhenNothingActivated_DoesNotWrite()
        {
            var repo = new FakeQuestsRepository();
            var cfg = QuestCfg("q1", new[] { Task(1, Tag("c")) }, activation: Tag("act"));

            var s = NewSession(repo, cfg);
            s.Parser.Register("act", false);
            s.Parser.Register("c", false);
            s.Load();

            s.Save();
            Assert.AreEqual(0, repo.SaveCallCount);
        }

        [Test]
        public void SavedQuest_SerializesStatesAsReadableStrings()
        {
            var dto = new SavedQuest
            {
                State = QuestState.Active,
                Tasks = new List<SavedQuestTask>
                {
                    new() { Id = 1, State = QuestTaskState.Active }
                }
            };

            var json = JsonConvert.SerializeObject(dto, Formatting.None);

            StringAssert.Contains("\"State\":\"Active\"", json);
            StringAssert.Contains("\"Tasks\":[{\"Id\":1,\"State\":\"Active\"}]", json);
        }
    }
}
