using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Game.Configs.Models;
using Game.Quest.API;
using Game.Quest.Services;
using Game.Quest.Tests.Editor.Fakes;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Quest.Tests.Editor
{
    public sealed class QuestsServiceTests
    {
        // ----- builders -----

        private static JObject Tag(string tag) => new JObject { ["tag"] = tag };

        private static QuestTaskConfig Task(int id, JObject completion, JObject activation = null)
            => new QuestTaskConfig { Id = id, CompletionConditions = completion, ActivationConditions = activation };

        private static QuestConfig QuestCfg(
            string id, QuestTaskConfig[] tasks, string chainId = null, string[] next = null,
            JObject activation = null, JObject fail = null, string type = "story")
            => new QuestConfig
            {
                Id = id, Type = type, ChainId = chainId, NextQuestIds = next,
                Tasks = tasks, ActivationConditions = activation, FailConditions = fail
            };

        private sealed class Harness
        {
            public QuestsService Service;
            public FakeConditionParser Parser;
            public FakeSalesStatsService Sales;
            public readonly List<string> Events = new();

            public void Load() => Service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private static Harness Build(params QuestConfig[] quests)
        {
            var h = new Harness { Parser = new FakeConditionParser(), Sales = new FakeSalesStatsService() };
            var configs = new FakeConfigsService();
            foreach (var q in quests) configs.Add(q);
            h.Service = new QuestsService(new FakeSaveService(), configs, h.Parser, sales: h.Sales);
            h.Service.QuestStarted += q => h.Events.Add($"started:{q.Id}");
            h.Service.QuestCompleted += q => h.Events.Add($"completed:{q.Id}");
            h.Service.QuestAwarded += q => h.Events.Add($"awarded:{q.Id}");
            h.Service.QuestFailed += q => h.Events.Add($"failed:{q.Id}");
            h.Service.TaskCompleted += t => h.Events.Add($"task:{t.QuestId}.{t.Id}");
            return h;
        }

        // ----- tests -----

        [Test]
        public void HeadWithoutActivation_AutoStarts()
        {
            var h = Build(QuestCfg("q1", new[] { Task(1, Tag("c1")) }));
            h.Parser.Register("c1", false);

            h.Load();

            Assert.AreEqual(QuestState.Active, h.Service.GetQuestState("q1"));
            Assert.AreEqual(1, h.Events.Count(e => e == "started:q1"));
        }

        [Test]
        public void Completion_AutoAwards_InEventOrder()
        {
            var h = Build(QuestCfg("q1", new[] { Task(1, Tag("c1")) }));
            var c1 = h.Parser.Register("c1", false);
            h.Load();

            c1.Met = true;
            h.Sales.RaiseChanged();

            Assert.AreEqual(QuestState.Awarded, h.Service.GetQuestState("q1"));
            var iCompleted = h.Events.IndexOf("completed:q1");
            var iAwarded = h.Events.IndexOf("awarded:q1");
            Assert.Greater(iCompleted, -1);
            Assert.Greater(iAwarded, iCompleted, "QuestCompleted must precede QuestAwarded");
        }

        [Test]
        public void TaskWithoutCompletionConditions_CompletesImmediately()
        {
            var h = Build(QuestCfg("q1", new[] { Task(1, completion: null) }));
            h.Load();
            Assert.AreEqual(QuestState.Awarded, h.Service.GetQuestState("q1"));
        }

        [Test]
        public void Chain_AwardForceActivatesSuccessor_IgnoringItsActivation()
        {
            var a = QuestCfg("a", new[] { Task(1, Tag("ca")) }, chainId: "ch", next: new[] { "b" });
            var b = QuestCfg("b", new[] { Task(1, Tag("cb")) }, chainId: "ch", activation: Tag("act_b"));
            var h = Build(a, b);
            var ca = h.Parser.Register("ca", false);
            h.Parser.Register("cb", false);
            h.Parser.Register("act_b", false); // successor activation never met — must be ignored
            h.Load();

            Assert.AreEqual(QuestState.Active, h.Service.GetQuestState("a"));
            Assert.AreEqual(QuestState.Pending, h.Service.GetQuestState("b"), "successor waits for predecessor");

            ca.Met = true;
            h.Sales.RaiseChanged();

            Assert.AreEqual(QuestState.Awarded, h.Service.GetQuestState("a"));
            Assert.AreEqual(QuestState.Active, h.Service.GetQuestState("b"), "chain link is a hard transition");
        }

        [Test]
        public void FailWinsOverCompletion()
        {
            var h = Build(QuestCfg("q1", new[] { Task(1, Tag("c")) }, fail: Tag("f")));
            h.Parser.Register("c", true);
            h.Parser.Register("f", true);

            h.Load();

            Assert.AreEqual(QuestState.Failed, h.Service.GetQuestState("q1"));
            CollectionAssert.DoesNotContain(h.Events, "awarded:q1");
        }

        [Test]
        public void TryAward_And_TryActivate_AreIdempotent()
        {
            var h = Build(QuestCfg("q1", new[] { Task(1, completion: null) })); // auto-awards on load
            h.Load();

            Assert.AreEqual(QuestState.Awarded, h.Service.GetQuestState("q1"));
            Assert.IsFalse(h.Service.TryAwardAsync("q1", CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsFalse(h.Service.TryActivateAsync("q1", CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void GetActiveQuests_ReturnsOnlyActive()
        {
            var active = QuestCfg("q1", new[] { Task(1, Tag("c1")) });
            var awarded = QuestCfg("q2", new[] { Task(1, completion: null) });
            var h = Build(active, awarded);
            h.Parser.Register("c1", false);
            h.Load();

            var ids = h.Service.GetActiveQuests().Select(q => q.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "q1" }, ids);
        }

        [Test]
        public void GetChain_OrdersByLinkage_WithFinalAndCurrent()
        {
            var a = QuestCfg("a", new[] { Task(1, Tag("ca")) }, chainId: "ch", next: new[] { "b" });
            var b = QuestCfg("b", new[] { Task(1, Tag("cb")) }, chainId: "ch");
            var h = Build(a, b);
            h.Parser.Register("ca", false);
            h.Parser.Register("cb", false);
            h.Load();

            var chain = h.Service.GetChain("ch");
            Assert.IsNotNull(chain);
            CollectionAssert.AreEqual(new[] { "a", "b" }, chain.Quests.Select(q => q.Id).ToList());
            Assert.AreEqual("b", chain.FinalQuest.Id);
            Assert.AreEqual("a", chain.CurrentQuest.Id); // a is Active
            Assert.AreSame(chain.Quests.First(), h.Service.GetChainByQuestId("b").Quests.First());
        }

        [Test]
        public void UnknownSuccessor_LogsError_NoCrash()
        {
            LogAssert.Expect(LogType.Error, new Regex("points to unknown successor 'missing'"));
            var a = QuestCfg("a", new[] { Task(1, Tag("ca")) }, chainId: "ch", next: new[] { "missing" });
            var h = Build(a);
            h.Parser.Register("ca", false);

            h.Load();

            Assert.AreEqual(QuestState.Active, h.Service.GetQuestState("a"));
        }

        [Test]
        public void Cycle_LogsError_NoCrash()
        {
            LogAssert.Expect(LogType.Error, new Regex("cycle detected"));
            LogAssert.Expect(LogType.Error, new Regex("cycle detected"));
            var a = QuestCfg("a", new[] { Task(1, Tag("ca")) }, chainId: "ch", next: new[] { "b" });
            var b = QuestCfg("b", new[] { Task(1, Tag("cb")) }, chainId: "ch", next: new[] { "a" });
            var h = Build(a, b);
            h.Parser.Register("ca", false);
            h.Parser.Register("cb", false);

            h.Load();

            // Both are successors of each other → neither auto-activates; no exception.
            Assert.AreEqual(QuestState.Pending, h.Service.GetQuestState("a"));
            Assert.AreEqual(QuestState.Pending, h.Service.GetQuestState("b"));
        }

        [Test]
        public void DoubleAfterLoad_DoesNotDoubleSubscribe()
        {
            var h = Build(QuestCfg("q1", new[] { Task(1, Tag("c1")) }));
            var c1 = h.Parser.Register("c1", false);
            h.Load();
            h.Load(); // re-init must not double-subscribe

            c1.Met = true;
            h.Sales.RaiseChanged();

            Assert.AreEqual(1, h.Events.Count(e => e == "awarded:q1"));
        }

        [Test]
        public void EmptyQuest_SkippedWithError()
        {
            LogAssert.Expect(LogType.Error, new Regex("has no tasks"));
            var h = Build(QuestCfg("q1", new QuestTaskConfig[0]));

            h.Load();

            Assert.IsNull(h.Service.TryGetQuest("q1"));
        }
    }
}
