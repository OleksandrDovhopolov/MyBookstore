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

namespace Book.Sell.Tests.Editor
{
    public sealed class QuestReplacingCustomerSpawnerTests
    {
        private static readonly SalesTuning Tuning = SalesTestKit.FastTuning();
        private static readonly SalesSessionSetup Setup = new(1, "loc", new[] { "book_fact" });

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
                    Options = Array.Empty<DialogueOptionConfig>()
                }
            }
        };

        private static IQuest ActiveQuestWithDialogue(string questId, string dialogueId, string characterId)
            => new FakeQuest(questId, new QuestConfig { Id = questId, DialogueId = dialogueId, CharacterId = characterId });

        [Test]
        public void ReplacesRegularSlot_WithQuestCharacter()
        {
            var configs = Configs();
            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q_intro_eddi", "eddy1", "eddi"));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new QuestReplacingCustomerSpawner(
                inner, configs, quests, new StubDeliveredDialogues(), new StubProfileProvider());
            var customers = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom());

            Assert.AreEqual(1, customers.Count, "Quest character replaces the only regular slot.");
            Assert.AreEqual("quest_q_intro_eddi", customers[0].Id);
            Assert.AreEqual("eddi", customers[0].CharacterId);
        }

        [Test]
        public void QuestCharacter_UsesCharacterFavoriteGenres()
        {
            var configs = Configs();
            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q_intro_eddi", "eddy1", "eddi"));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new QuestReplacingCustomerSpawner(
                inner, configs, quests, new StubDeliveredDialogues(), new StubProfileProvider());
            var customers = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom());

            CollectionAssert.AreEqual(new[] { "Fact", "Travel" }, customers[0].Profile.DesiredGenres);
        }

        [Test]
        public void QuestCharacter_UsesScriptedPassiveCount()
        {
            var configs = Configs(script: new[]
            {
                new ScriptedPassivePurchaseConfig { Genre = "Fact", ForceHit = true },
                new ScriptedPassivePurchaseConfig { Genre = "Travel", ForceHit = false }
            });
            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q_intro_eddi", "eddy1", "eddi"));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new QuestReplacingCustomerSpawner(
                inner, configs, quests, new StubDeliveredDialogues(), new StubProfileProvider());
            var customer = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom())[0];
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
        public void MissingFavoriteGenres_FallsBackToProfileProvider()
        {
            var configs = Configs(characterGenres: Array.Empty<string>());
            var quests = new FakeQuestsService(ActiveQuestWithDialogue("q_intro_eddi", "eddy1", "eddi"));
            var inner = new StubCustomerSpawner(new List<Customer> { Passive("inner_1") });

            var spawner = new QuestReplacingCustomerSpawner(
                inner, configs, quests, new StubDeliveredDialogues(), new StubProfileProvider());
            var customers = spawner.BuildCustomers(Setup, Tuning, new FakeSalesRandom());

            CollectionAssert.AreEqual(new[] { "Fallback" }, customers[0].Profile.DesiredGenres);
        }

        private static FakeConfigsService Configs(
            string[] characterGenres = null,
            ScriptedPassivePurchaseConfig[] script = null)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SingleNodeDialogue("eddy1") });
            configs.SetAll(new[] { SalesTestKit.Book("book_fact", "Fact"), SalesTestKit.Book("book_travel", "Travel") });
            configs.SetAll(new[]
            {
                new CharacterConfig
                {
                    Id = "eddi",
                    FavoriteGenres = characterGenres ?? new[] { "Fact", "Travel" },
                    ScriptedPassivePurchases = script
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
            public bool IsDelivered(string dialogueId) => false;

            public UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct) => UniTask.CompletedTask;
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
