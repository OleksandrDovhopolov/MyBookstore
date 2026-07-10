using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Quest.API;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// GAME-6 quest-driven scheduling. Unit-level: the decorator prepends one quest character per ACTIVE
    /// quest carrying a (not-yet-delivered, resolvable) dialogue; unknown dialogue → warn+skip; already
    /// delivered → skip; no such quest → inner unchanged. Controller-smoke: an active quest with a dialogue
    /// opens through the real controller and plays to the end. The day config is never consulted.
    /// </summary>
    public sealed class QuestSchedulingCustomerSpawnerTests
    {
        private static readonly SalesTuning Tuning = SalesTestKit.FastTuning();
        private static readonly SalesSessionSetup Setup = new(1, "loc", Array.Empty<string>());

        private static Customer Passive(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new PassivePurchaseStep(), new LeaveStep() });

        private static DialogueConfig SingleNodeDialogue(string id) => new()
        {
            Id = id,
            Nodes = new[]
            {
                new DialogueNodeConfig
                {
                    NodeId = "root",
                    Lines = new[] { new DialogueLineConfig { Speaker = "x", Text = "line" } },
                    Options = new[] { new DialogueOptionConfig { Text = "ok", Next = "end" } }
                }
            }
        };

        private static IQuest ActiveQuestWithDialogue(string questId, string dialogueId)
            => new FakeQuest(questId, new QuestConfig { Id = questId, DialogueId = dialogueId });

        // --- decorator unit tests -----------------------------------------------------------

        [Test]
        public void PrependsQuestCharacter_PerActiveQuestWithDialogue()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SingleNodeDialogue("dlg") });
            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q1", "dlg"));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1"), Passive("inner_2") });

            var spawner = new QuestSchedulingCustomerSpawner(inner, configs, quests, new StubDeliveredDialogues());
            var customers = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(3, customers.Count, "inner (2) + 1 quest character.");
            Assert.AreEqual("quest_q1", customers[0].Id, "Quest character (id from quest) is prepended.");
            Assert.AreEqual("inner_1", customers[1].Id);
        }

        [Test]
        public void UnknownDialogueConfig_WarnsAndSkips()
        {
            var configs = new FakeConfigsService(); // no DialogueConfig registered
            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q1", "nope"));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new QuestSchedulingCustomerSpawner(inner, configs, quests, new StubDeliveredDialogues());

            LogAssert.Expect(LogType.Warning,
                "[Sales.QuestSchedule] quest 'q1' references dialogue 'nope' with no DialogueConfig — skipped.");
            var customers = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count, "Unknown dialogue adds no quest character.");
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void AlreadyDelivered_Skips()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SingleNodeDialogue("dlg") });
            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q1", "dlg"));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new QuestSchedulingCustomerSpawner(
                inner, configs, quests, new StubDeliveredDialogues("dlg")); // dlg already delivered
            var customers = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count, "Delivered dialogue does not respawn its character.");
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void NoActiveQuestWithDialogue_ReturnsInnerUnchanged()
        {
            var configs = new FakeConfigsService();
            // Active quest but no DialogueId → nothing to schedule.
            var quests = new FakeQuestsService(new FakeQuest("q1", new QuestConfig { Id = "q1", DialogueId = null }));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new QuestSchedulingCustomerSpawner(inner, configs, quests, new StubDeliveredDialogues());
            var customers = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        // --- controller-smoke: active quest → dialogue opens → completes -------------------

        [Test]
        public void ActiveQuestDialogue_OpensThroughController_AndCompletes()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1") });
            configs.SetAll(new[] { SalesTestKit.Location() });
            configs.SetAll(new[] { SingleNodeDialogue("dlg") });

            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q1", "dlg"));
            // Empty inner → the quest character is the only customer, isolating the scheduled path.
            var spawner = new QuestSchedulingCustomerSpawner(
                new StubCustomerSpawner(Array.Empty<Customer>()), configs, quests, new StubDeliveredDialogues());

            var controller = new SalesDayController(
                configs,
                new DefaultSalesSetupProvider(configs),
                new RecommendationScoringService(),
                SalesTestKit.LegacyResolver(),
                new FakeSalesRandom(),
                spawner,
                new InteractionLock(),
                Tuning,
                shelfBuilder: new SalesShelfBuilder(configs));

            var dialogueCount = 0;
            string startedId = null;
            controller.DialogueStarted += (_, payload) => { dialogueCount++; startedId = payload.DialogueId; };

            controller.StartDayAsync(1, CancellationToken.None).GetAwaiter().GetResult();
            for (var i = 0; i < 50 && dialogueCount == 0 && controller.Phase == SalesDayPhase.Running; i++)
                controller.Tick(0.1f);

            Assert.AreEqual(1, dialogueCount, "The prepended quest character opened its dialogue.");
            Assert.AreEqual("dlg", startedId);

            controller.CompleteDialogue();
            for (var i = 0; i < 200 && controller.Phase == SalesDayPhase.Running; i++)
                controller.Tick(0.1f);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, controller.Phase,
                "After CompleteDialogue the quest character finishes and the day becomes concludable.");
        }

        // --- fakes ---------------------------------------------------------------------------

        private sealed class StubDeliveredDialogues : IDeliveredDialoguesService
        {
            private readonly HashSet<string> _delivered;
            public StubDeliveredDialogues(params string[] delivered) => _delivered = new HashSet<string>(delivered);

            public bool IsDelivered(string dialogueId) => _delivered.Contains(dialogueId);

            public UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct)
            {
                _delivered.Add(dialogueId);
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeQuestsService : IQuestsService
        {
            private readonly List<IQuest> _active;
            public FakeQuestsService(params IQuest[] active) => _active = new List<IQuest>(active);

            public IEnumerable<IQuest> GetActiveQuests() => _active;

            public IQuest TryGetQuest(string questId) => _active.Find(q => q.Id == questId);
            public QuestConfig GetQuestConfig(string questId) => TryGetQuest(questId)?.Config;
            public QuestState GetQuestState(string questId) => default;
            public IQuestChain GetChain(string chainId) => null;
            public IQuestChain GetChainByQuestId(string questId) => null;
            public UniTask<bool> TryActivateAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryAwardAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryFailAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);

            public event Action<IQuest> QuestStarted { add { } remove { } }
            public event Action<IQuest> QuestCompleted { add { } remove { } }
            public event Action<IQuest> QuestAwarded { add { } remove { } }
            public event Action<IQuest> QuestFailed { add { } remove { } }
            public event Action<IQuestTask> TaskCompleted { add { } remove { } }
            public event Action<IQuestTask> TaskProgressChanged { add { } remove { } }
        }

        private sealed class FakeQuest : IQuest
        {
            public FakeQuest(string id, QuestConfig config) { Id = id; Config = config; }

            public string Id { get; }
            public QuestType Type => default;
            public QuestState State => default;
            public string ChainId => Config?.ChainId;
            public string CharacterId => Config?.CharacterId;
            public QuestConfig Config { get; }
            public IReadOnlyList<IQuestTask> Tasks => Array.Empty<IQuestTask>();
            public IQuestTask GetTask(int id) => null;
        }
    }
}
