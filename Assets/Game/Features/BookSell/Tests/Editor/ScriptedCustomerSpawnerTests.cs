using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
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
    public sealed class ScriptedCustomerSpawnerTests
    {
        private static readonly SalesTuning Tuning = SalesTestKit.FastTuning();
        private static readonly SalesSessionSetup DayTwoSetup = new(2, "loc", new[] { "book_travel" });
        private static readonly SalesSessionSetup DayOneSetup = new(1, "loc", new[] { "book_fact", "book_travel" });

        private static Customer Passive(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new PassivePurchaseStep(), new LeaveStep() });

        private static CustomerScriptConfig Script(
            string id,
            int? dayIndex = 2,
            string activationQuestId = null,
            string dialogueId = null,
            string characterId = null,
            ScriptedPassivePurchaseConfig[] attempts = null)
            => new()
            {
                Id = id,
                DayIndex = dayIndex,
                ActivationQuestId = activationQuestId,
                DialogueId = dialogueId,
                CharacterId = characterId,
                PassiveAttempts = attempts ?? new[]
                {
                    new ScriptedPassivePurchaseConfig { Genre = "Travel", ForceHit = false }
                }
            };

        private static ScriptedPassivePurchaseConfig[] EddiAttempts() => new[]
        {
            new ScriptedPassivePurchaseConfig { Genre = "Fact", ForceHit = true },
            new ScriptedPassivePurchaseConfig { Genre = "Travel", ForceHit = false }
        };

        private static DialogueConfig SingleNodeDialogue(string id) => new()
        {
            Id = id,
            Nodes = new[]
            {
                new DialogueNodeConfig
                {
                    NodeId = "root",
                    Lines = new[] { new DialogueLineConfig { Speaker = "x", Text = "line" } },
                    Options = Array.Empty<DialogueOptionConfig>()
                }
            }
        };

        private static ScriptedCustomerSpawner Spawner(
            ICustomerSpawner inner,
            FakeConfigsService configs,
            FakeQuestsService quests = null,
            StubDeliveredDialogues delivered = null,
            ICustomerProfileProvider profiles = null)
            => new(
                inner,
                configs,
                quests ?? new FakeQuestsService(),
                delivered ?? new StubDeliveredDialogues(),
                profiles ?? new StubProfileProvider());

        [Test]
        public void DayScript_ReplacesRegularSlot_AndPreservesTotalCount()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("day2_missed_sale") });
            var inner = new StubCustomerSpawner(new List<Customer>
            {
                Passive("inner_1"),
                Passive("inner_2"),
                Passive("inner_3")
            });

            var spawner = Spawner(inner, configs);
            var customers = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(3, customers.Count, "Scripted customer replaces a regular slot.");
            Assert.AreEqual("script_day2_missed_sale", customers[0].Id);
            Assert.AreEqual("inner_2", customers[1].Id);
            Assert.AreEqual("inner_3", customers[2].Id);
        }

        [Test]
        public void NonMatchingDay_ReturnsInnerUnchanged()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("day2_missed_sale") });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = Spawner(inner, configs);
            var customers = spawner.BuildCustomers(new SalesSessionSetup(3, "loc", Array.Empty<string>()),
                Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void ScriptedCustomer_IsFirst_AndCarriesForcedMissPlan()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("day2_missed_sale", characterId: "visitor_day2") });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1"), Passive("inner_2") });

            var spawner = Spawner(inner, configs);
            var customer = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom())[0];

            Assert.AreEqual("script_day2_missed_sale", customer.Id);
            Assert.AreEqual("visitor_day2", customer.CharacterId);
            Assert.IsTrue(customer.TryConsumeNextScriptedPassiveAttempt(out var attempt));
            Assert.AreEqual("Travel", attempt.Genre);
            Assert.IsFalse(attempt.ForceHit);
            Assert.IsFalse(customer.TryConsumeNextScriptedPassiveAttempt(out _));
        }

        [Test]
        public void EmptyPassiveAttempts_WarnsAndSkips()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("empty", attempts: Array.Empty<ScriptedPassivePurchaseConfig>()) });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = Spawner(inner, configs);

            LogAssert.Expect(LogType.Warning,
                "[Sales.CustomerScript] script 'empty' has no passive attempts; skipped.");
            var customers = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void DialogueScriptWithoutPassiveAttempts_StillSpawns_AndDoesNotReplaceRegularSlot()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new CustomerScriptConfig
                {
                    Id = "milly_intro",
                    DayIndex = 2,
                    CharacterId = "milly",
                    DialogueId = "milly1"
                }
            });
            configs.SetAll(new[] { SingleNodeDialogue("milly1") });
            configs.SetAll(new[] { SalesTestKit.Book("book_drama", "Drama") });
            configs.SetAll(new[]
            {
                new CharacterConfig
                {
                    Id = "milly",
                    FavoriteGenres = new[] { "Drama" }
                }
            });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });
            var tuning = SalesTestKit.FastTuning();

            var customers = Spawner(inner, configs).BuildCustomers(DayTwoSetup, tuning, new FakeSalesRandom());
            var customer = customers[0];
            var ctx = SalesTestKit.Context(SalesTestKit.Shelf(), SalesTestKit.Location(), new RecordingSink());

            Assert.AreEqual(2, customers.Count, "Dialogue-only story visits do not consume regular sales slots.");
            Assert.AreEqual("script_milly_intro", customer.Id);
            Assert.AreEqual("inner_1", customers[1].Id);
            Assert.AreEqual("milly", customer.CharacterId);
            Assert.IsNull(customer.ScriptedPassivePlan);

            customer.Tick(ctx, 1f); // Approach -> Dialog
            customer.Tick(ctx, 1f); // Dialog acquires lock
            customer.ForceCompleteCurrentStep(ctx);

            Assert.IsInstanceOf<CompletePurchaseStep>(customer.CurrentStep);
        }

        [Test]
        public void MoreScriptsThanSlots_WarnsAndSkipsOverflow()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { Script("first"), Script("second") });
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = Spawner(inner, configs);

            LogAssert.Expect(LogType.Warning,
                "[Sales.CustomerScript] replacement capacity exhausted for day=2; extra customer scripts skipped.");
            var customers = spawner.BuildCustomers(DayTwoSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("script_first", customers[0].Id);
        }

        [Test]
        public void QuestScript_ReplacesRegularSlot_WhenActivationQuestIsActive()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_intro_eddi", dialogueId: "eddy1", characterId: "eddi",
                attempts: EddiAttempts()));
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var customers = Spawner(inner, configs, quests).BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count, "Quest script replaces the regular slot.");
            Assert.AreEqual("script_eddi_intro", customers[0].Id);
            Assert.AreEqual("eddi", customers[0].CharacterId);
        }

        [Test]
        public void DayOneScripts_PlaceEddiInSlotZero_AndMissNpcInSlotOne()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                Script("eddi_intro", dayIndex: null,
                    activationQuestId: "q_intro_eddi", dialogueId: "eddy1", characterId: "eddi",
                    attempts: EddiAttempts()),
                Script("day2_missed_sale", dayIndex: 1)
            });
            configs.SetAll(new[] { SingleNodeDialogue("eddy1") });
            configs.SetAll(new[] { SalesTestKit.Book("book_fact", "Fact"), SalesTestKit.Book("book_travel", "Travel") });
            configs.SetAll(new[]
            {
                new CharacterConfig
                {
                    Id = "eddi",
                    FavoriteGenres = new[] { "Fact", "Travel" }
                }
            });
            var inner = new StubCustomerSpawner(new List<Customer>
            {
                Passive("inner_1"),
                Passive("inner_2"),
                Passive("inner_3"),
                Passive("inner_4")
            });
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));

            var customers = Spawner(inner, configs, quests).BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(4, customers.Count);
            Assert.AreEqual("script_eddi_intro", customers[0].Id);
            Assert.AreEqual("eddi", customers[0].CharacterId);
            Assert.AreEqual("script_day2_missed_sale", customers[1].Id);
            Assert.IsNull(customers[1].CharacterId);
        }

        [Test]
        public void QuestScript_Skips_WhenDialogueAlreadyDelivered()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_intro_eddi", dialogueId: "eddy1", characterId: "eddi",
                attempts: EddiAttempts()));
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var customers = Spawner(inner, configs, quests, new StubDeliveredDialogues("eddy1"))
                .BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void QuestScript_Skips_WhenActivationQuestIsNotActive()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_intro_eddi", dialogueId: "eddy1", characterId: "eddi",
                attempts: EddiAttempts()));
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Pending));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var customers = Spawner(inner, configs, quests).BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void QuestScript_UnknownQuest_WarnsAndSkips()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_missing", dialogueId: "eddy1", characterId: "eddi",
                attempts: EddiAttempts()));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            LogAssert.Expect(LogType.Warning,
                "[Sales.CustomerScript] script 'eddi_intro' references unknown activation quest 'q_missing'; skipped.");
            var customers = Spawner(inner, configs).BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void QuestScript_MissingDialogue_WarnsAndSkips()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_intro_eddi", dialogueId: "missing", characterId: "eddi",
                attempts: EddiAttempts()), includeDialogue: false);
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            LogAssert.Expect(LogType.Warning,
                "[Sales.CustomerScript] script 'eddi_intro' references dialogue 'missing' with no DialogueConfig; skipped.");
            var customers = Spawner(inner, configs, quests).BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        [Test]
        public void QuestScript_UsesCharacterFavoriteGenres()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_intro_eddi", dialogueId: "eddy1", characterId: "eddi",
                attempts: EddiAttempts()));
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var customers = Spawner(inner, configs, quests).BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            CollectionAssert.AreEqual(new[] { "Fact", "Travel" }, customers[0].Profile.DesiredGenres);
        }

        [Test]
        public void QuestScript_CarriesFactHitThenTravelMiss()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_intro_eddi", dialogueId: "eddy1", characterId: "eddi",
                attempts: EddiAttempts()));
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var customer = Spawner(inner, configs, quests)
                .BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom())[0];

            Assert.IsTrue(customer.TryConsumeNextScriptedPassiveAttempt(out var first));
            Assert.AreEqual("Fact", first.Genre);
            Assert.IsTrue(first.ForceHit);

            Assert.IsTrue(customer.TryConsumeNextScriptedPassiveAttempt(out var second));
            Assert.AreEqual("Travel", second.Genre);
            Assert.IsFalse(second.ForceHit);

            Assert.IsFalse(customer.TryConsumeNextScriptedPassiveAttempt(out _));
        }

        [Test]
        public void QuestScript_BuildsDialogueStepFollowedByScriptedPassiveCount()
        {
            var configs = ConfigsWithEddi(Script("eddi_intro", dayIndex: null,
                activationQuestId: "q_intro_eddi", dialogueId: "eddy1", characterId: "eddi",
                attempts: EddiAttempts()));
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });
            var customer = Spawner(inner, configs, quests)
                .BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom())[0];
            var ctx = SalesTestKit.Context(SalesTestKit.Shelf(), SalesTestKit.Location(), new RecordingSink());

            customer.Tick(ctx, 1f); // Approach -> Dialog
            customer.Tick(ctx, 1f); // Dialog acquires lock
            customer.ForceCompleteCurrentStep(ctx);

            Assert.IsInstanceOf<PassivePurchaseStep>(customer.CurrentStep);
            customer.ForceCompleteCurrentStep(ctx);

            Assert.IsInstanceOf<PassivePurchaseStep>(customer.CurrentStep);
            customer.ForceCompleteCurrentStep(ctx);

            Assert.IsInstanceOf<CompletePurchaseStep>(customer.CurrentStep);
        }

        [Test]
        public void InvalidSchedule_WarnsAndSkips()
        {
            var configs = ConfigsWithEddi(Script("bad", dayIndex: 1, activationQuestId: "q_intro_eddi"));
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Active));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            LogAssert.Expect(LogType.Warning,
                "[Sales.CustomerScript] script 'bad' must set exactly one of dayIndex or activationQuestId; skipped.");
            var customers = Spawner(inner, configs, quests).BuildCustomers(DayOneSetup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count);
            Assert.AreEqual("inner_1", customers[0].Id);
        }

        private static FakeConfigsService ConfigsWithEddi(CustomerScriptConfig script, bool includeDialogue = true)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { script });
            if (includeDialogue)
                configs.SetAll(new[] { SingleNodeDialogue("eddy1") });
            configs.SetAll(new[] { SalesTestKit.Book("book_fact", "Fact"), SalesTestKit.Book("book_travel", "Travel") });
            configs.SetAll(new[]
            {
                new CharacterConfig
                {
                    Id = "eddi",
                    FavoriteGenres = new[] { "Fact", "Travel" }
                }
            });
            return configs;
        }

        private sealed class StubProfileProvider : ICustomerProfileProvider
        {
            public CustomerProfile Create(SalesSessionSetup setup, ISalesRandom random)
                => new(new[] { "Fallback" });
        }

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

            public UniTask MarkDeliveredDeferredAsync(string dialogueId, CancellationToken ct)
            {
                _delivered.Add(dialogueId);
                return UniTask.CompletedTask;
            }

            public UniTask CommitAsync(CancellationToken ct) => UniTask.CompletedTask;

            public void DiscardDeferred() { }
        }

        private sealed class FakeQuestsService : IQuestsService
        {
            private readonly Dictionary<string, QuestState> _states = new(StringComparer.Ordinal);

            public FakeQuestsService(params (string id, QuestState state)[] quests)
            {
                for (var i = 0; i < quests.Length; i++)
                    _states[quests[i].id] = quests[i].state;
            }

            public IQuest TryGetQuest(string questId)
                => _states.ContainsKey(questId) ? new FakeQuest(questId, _states[questId]) : null;

            public QuestConfig GetQuestConfig(string questId) => TryGetQuest(questId)?.Config;
            public QuestState GetQuestState(string questId) => _states.TryGetValue(questId, out var state) ? state : QuestState.Pending;
            public IReadOnlyList<IQuest> GetAllQuests() => Array.Empty<IQuest>();
            public IEnumerable<IQuest> GetActiveQuests() => Array.Empty<IQuest>();
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
            public FakeQuest(string id, QuestState state)
            {
                Id = id;
                State = state;
                Config = new QuestConfig { Id = id };
            }

            public string Id { get; }
            public QuestType Type => default;
            public QuestState State { get; }
            public string ChainId => Config?.ChainId;
            public QuestConfig Config { get; }
            public IReadOnlyList<IQuestTask> Tasks => Array.Empty<IQuestTask>();
            public IQuestTask GetTask(int id) => null;
        }
    }
}
