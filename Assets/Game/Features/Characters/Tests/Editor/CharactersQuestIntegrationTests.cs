using System.Collections.Generic;
using System.Threading;
using Game.Characters.API;
using Game.Characters.Services;
using Game.Characters.Services.Persistence;
using Game.Characters.Tests.Editor.Fakes;
using Game.Configs.Models;
using Game.Quest.API;
using NUnit.Framework;

namespace Game.Characters.Tests.Editor
{
    public sealed class CharactersQuestIntegrationTests
    {
        // ----- helpers -----

        private static CharacterConfig Character(string id, CharacterMemoryConfig[] memories = null,
            string[] discoveryQuestIds = null, string[] discoveryChainIds = null)
            => new()
            {
                Id = id,
                DisplayNameKey = $"character.{id}.name",
                Memories = memories,
                DiscoveryQuestIds = discoveryQuestIds,
                DiscoveryQuestChainIds = discoveryChainIds,
            };

        private static CharacterMemoryConfig MemoryByQuest(string id, string questId, bool golden = false)
            => new() { Id = id, QuestId = questId, IsGolden = golden };

        private static CharacterMemoryConfig MemoryByChain(string id, string chainId)
            => new() { Id = id, QuestChainId = chainId };

        private sealed class Harness
        {
            public CharactersService Service;
            public FakeQuestsService Quests;
            public readonly List<string> Discovered = new();
            public readonly List<string> Unlocked = new();
        }

        private static Harness Load(FakeQuestsService quests, ICharactersRepository repo,
            params CharacterConfig[] configs)
        {
            var save = new FakeSaveService();
            var cfgs = new FakeConfigsService();
            foreach (var c in configs) cfgs.Add(c);

            var h = new Harness { Quests = quests };
            h.Service = new CharactersService(save, cfgs, quests, repo);
            h.Service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            // Subscribe AFTER load so reconcile-on-load never feeds the counters.
            h.Service.CharacterDiscovered += c => h.Discovered.Add(c.Id);
            h.Service.MemoryUnlocked += m => h.Unlocked.Add(m.Id);
            return h;
        }

        // ----- discovery via live events -----

        [Test]
        public void QuestStarted_OfMemoryQuest_RaisesCharacterDiscoveredOnce()
        {
            var quests = new FakeQuestsService();
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", new[] { MemoryByQuest("m1", "q1") }));

            quests.SetState("q1", QuestState.Active);
            quests.RaiseStarted(new FakeQuest("q1", QuestState.Active));

            Assert.AreEqual(new[] { "harper" }, h.Discovered.ToArray());
            Assert.IsTrue(h.Service.IsDiscovered("harper"));
        }

        [Test]
        public void IntroQuest_WithoutMemory_DiscoversViaDiscoveryQuestIds()
        {
            // Live event path.
            var quests = new FakeQuestsService();
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", memories: null, discoveryQuestIds: new[] { "intro" }));

            quests.SetState("intro", QuestState.Active);
            quests.RaiseStarted(new FakeQuest("intro", QuestState.Active));

            Assert.IsTrue(h.Service.IsDiscovered("harper"));
            Assert.AreEqual(1, h.Discovered.Count);
        }

        [Test]
        public void IntroQuest_AlreadyStartedBeforeLoad_DiscoversOnReconcile_NoEvent()
        {
            var quests = new FakeQuestsService().SetState("intro", QuestState.Active);
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", memories: null, discoveryQuestIds: new[] { "intro" }));

            Assert.IsTrue(h.Service.IsDiscovered("harper"));
            Assert.IsEmpty(h.Discovered); // seeded silently
        }

        // ----- memory unlock via live events -----

        [Test]
        public void QuestAwarded_ByQuestId_RaisesMemoryUnlockedOnce()
        {
            var quests = new FakeQuestsService();
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", new[] { MemoryByQuest("m1", "q1") }));

            quests.SetState("q1", QuestState.Awarded);
            quests.RaiseAwarded(new FakeQuest("q1", QuestState.Awarded));

            Assert.AreEqual(new[] { "m1" }, h.Unlocked.ToArray());
            Assert.IsTrue(h.Service.IsMemoryUnlocked("harper", "m1"));
        }

        [Test]
        public void QuestAwarded_FinalChainQuest_RaisesChainMemoryUnlocked_RoutedByChainId()
        {
            var quests = new FakeQuestsService();
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", new[] { MemoryByChain("m1", "chain") }));

            quests.AddChain("chain",
                new FakeQuest("c1", QuestState.Awarded, "chain"),
                new FakeQuest("c2", QuestState.Awarded, "chain"));
            quests.RaiseAwarded(new FakeQuest("c2", QuestState.Awarded, "chain"));

            Assert.AreEqual(new[] { "m1" }, h.Unlocked.ToArray());
            Assert.IsTrue(h.Service.IsMemoryUnlocked("harper", "m1"));
        }

        [Test]
        public void QuestAwarded_Repeated_DoesNotDuplicateMemoryUnlocked()
        {
            var quests = new FakeQuestsService();
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", new[] { MemoryByQuest("m1", "q1") }));

            quests.SetState("q1", QuestState.Awarded);
            quests.RaiseAwarded(new FakeQuest("q1", QuestState.Awarded));
            quests.RaiseAwarded(new FakeQuest("q1", QuestState.Awarded));

            Assert.AreEqual(1, h.Unlocked.Count);
        }

        [Test]
        public void AwardedBeforeLoad_SeededOnReconcile_NoEvent()
        {
            var quests = new FakeQuestsService().SetState("q1", QuestState.Awarded);
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", new[] { MemoryByQuest("m1", "q1") }));

            Assert.IsTrue(h.Service.IsMemoryUnlocked("harper", "m1"));
            Assert.IsEmpty(h.Unlocked);
            Assert.IsEmpty(h.Discovered);
        }

        [Test]
        public void Discovery_HappensOnAwarded_WhenStartedWasMissed()
        {
            var quests = new FakeQuestsService();
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", new[] { MemoryByQuest("m1", "q1") }));

            // No QuestStarted; jump straight to Awarded.
            quests.SetState("q1", QuestState.Awarded);
            quests.RaiseAwarded(new FakeQuest("q1", QuestState.Awarded));

            Assert.IsTrue(h.Service.IsDiscovered("harper"));
            Assert.AreEqual(new[] { "harper" }, h.Discovered.ToArray());
        }

        // ----- persistence / ledger fallback -----

        [Test]
        public void LedgerSurvivesReload_AndUnlocksEvenWhenQuestStateReset()
        {
            var save = new FakeSaveService();
            var cfgs = new FakeConfigsService().Add(
                Character("harper", new[] { MemoryByQuest("m1", "q1") }));

            // Session 1: award the quest, flush save.
            var quests1 = new FakeQuestsService().SetState("q1", QuestState.Awarded);
            var svc1 = new CharactersService(save, cfgs, quests1, new SaveBackedCharactersRepository(save));
            svc1.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            svc1.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Session 2: same save, but quest state is back to Pending (e.g. quest config churn).
            var quests2 = new FakeQuestsService(); // all Pending
            var svc2 = new CharactersService(save, cfgs, quests2, new SaveBackedCharactersRepository(save));
            svc2.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(svc2.IsMemoryUnlocked("harper", "m1"), "ledger should keep memory unlocked");
            Assert.IsTrue(svc2.IsDiscovered("harper"), "discovered flag should persist");
        }

        // ----- lifecycle -----

        [Test]
        public void Dispose_Unsubscribes_NoEventsAfterDispose()
        {
            var quests = new FakeQuestsService();
            var h = Load(quests, new FakeCharactersRepository(),
                Character("harper", new[] { MemoryByQuest("m1", "q1") }));

            h.Service.Dispose();

            quests.SetState("q1", QuestState.Awarded);
            Assert.DoesNotThrow(() => quests.RaiseAwarded(new FakeQuest("q1", QuestState.Awarded)));
            Assert.IsEmpty(h.Unlocked);
            Assert.IsEmpty(h.Discovered);
        }
    }
}
