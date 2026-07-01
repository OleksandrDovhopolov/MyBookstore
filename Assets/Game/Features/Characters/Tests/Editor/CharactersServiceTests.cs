using System.Linq;
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
    public sealed class CharactersServiceTests
    {
        // ----- helpers -----

        private static CharacterConfig Character(string id, params CharacterMemoryConfig[] memories)
            => new()
            {
                Id = id,
                DisplayNameKey = $"character.{id}.name",
                RoleKey = $"character.{id}.role",
                DescriptionKey = $"character.{id}.desc",
                Memories = memories,
            };

        private static CharacterMemoryConfig MemoryByQuest(string id, string questId, bool golden = false)
            => new() { Id = id, QuestId = questId, IsGolden = golden, TitleKey = $"m.{id}.t" };

        private static CharacterMemoryConfig MemoryByChain(string id, string chainId, bool golden = false)
            => new() { Id = id, QuestChainId = chainId, IsGolden = golden, TitleKey = $"m.{id}.t" };

        private static CharactersService Build(
            FakeQuestsService quests,
            FakeCharactersRepository repo,
            params CharacterConfig[] configs)
        {
            var save = new FakeSaveService();
            var cfgs = new FakeConfigsService();
            foreach (var c in configs) cfgs.Add(c);

            var service = new CharactersService(save, cfgs, quests, repo);
            service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return service;
        }

        // ----- catalog -----

        [Test]
        public void GetAllCharacters_ReturnsEveryConfig()
        {
            var service = Build(new FakeQuestsService(), new FakeCharactersRepository(),
                Character("harper"), Character("walt"));

            var ids = service.GetAllCharacters().Select(c => c.Id).OrderBy(x => x).ToArray();

            CollectionAssert.AreEqual(new[] { "harper", "walt" }, ids);
        }

        [Test]
        public void TryGetCharacter_Unknown_ReturnsNull()
        {
            var service = Build(new FakeQuestsService(), new FakeCharactersRepository(), Character("harper"));
            Assert.IsNull(service.TryGetCharacter("nobody"));
        }

        // ----- memory unlock derivation -----

        [Test]
        public void MemoryByQuest_Awarded_Unlocked()
        {
            var quests = new FakeQuestsService().SetState("q1", QuestState.Awarded);
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "q1")));

            Assert.IsTrue(service.IsMemoryUnlocked("harper", "m1"));
        }

        [Test]
        public void MemoryByQuest_NotAwarded_Locked()
        {
            var quests = new FakeQuestsService().SetState("q1", QuestState.Active);
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "q1")));

            Assert.IsFalse(service.IsMemoryUnlocked("harper", "m1"));
        }

        [Test]
        public void MemoryByChain_FinalQuestAwarded_Unlocked()
        {
            var quests = new FakeQuestsService().AddChain("chain",
                new FakeQuest("c1", QuestState.Awarded, "chain"),
                new FakeQuest("c2", QuestState.Awarded, "chain"));
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByChain("m1", "chain")));

            Assert.IsTrue(service.IsMemoryUnlocked("harper", "m1"));
        }

        [Test]
        public void MemoryByChain_FinalQuestNotAwarded_Locked()
        {
            var quests = new FakeQuestsService().AddChain("chain",
                new FakeQuest("c1", QuestState.Awarded, "chain"),
                new FakeQuest("c2", QuestState.Active, "chain"));
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByChain("m1", "chain")));

            Assert.IsFalse(service.IsMemoryUnlocked("harper", "m1"));
        }

        [Test]
        public void UnknownQuestAndChain_Locked_NoThrow()
        {
            var service = Build(new FakeQuestsService(), new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "ghost"), MemoryByChain("m2", "ghost_chain")));

            Assert.IsFalse(service.IsMemoryUnlocked("harper", "m1"));
            Assert.IsFalse(service.IsMemoryUnlocked("harper", "m2"));
        }

        [Test]
        public void IsGolden_PassedThroughToReadModel()
        {
            var service = Build(new FakeQuestsService(), new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "q1", golden: true)));

            var memory = service.TryGetCharacter("harper").Memories.Single();
            Assert.IsTrue(memory.IsGolden);
        }

        // ----- discovery derivation -----

        [Test]
        public void Discovered_DefaultsFalse_WhenNoSaveAndNoQuestProgress()
        {
            var service = Build(new FakeQuestsService(), new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "q1")));

            Assert.IsFalse(service.IsDiscovered("harper"));
            CollectionAssert.IsEmpty(service.GetDiscoveredCharacters());
        }

        [Test]
        public void Discovered_FromSaveFlag()
        {
            var repo = new FakeCharactersRepository();
            repo.Stored = new SavedCharacters();
            repo.Stored.Characters["harper"] = new SavedCharacter { Discovered = true };

            var service = Build(new FakeQuestsService(), repo,
                Character("harper", MemoryByQuest("m1", "q1")));

            Assert.IsTrue(service.IsDiscovered("harper"));
            Assert.AreEqual(new[] { "harper" }, service.GetDiscoveredCharacters().Select(c => c.Id).ToArray());
        }

        [Test]
        public void Discovered_FromOwningQuestProgress()
        {
            // Owning quest has started (Active) but not awarded → memory locked, character discovered.
            var quests = new FakeQuestsService().SetState("q1", QuestState.Active);
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "q1")));

            Assert.IsTrue(service.IsDiscovered("harper"));
            Assert.IsFalse(service.IsMemoryUnlocked("harper", "m1"));
        }

        // ----- journal read model -----

        [Test]
        public void GetJournalEntry_ProjectsMemoryStateAndLinkedQuest()
        {
            var quests = new FakeQuestsService().SetState("q1", QuestState.Awarded);
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "q1", golden: true)));

            var entry = service.GetJournalEntry("harper");

            Assert.AreEqual("harper", entry.CharacterId);
            Assert.IsTrue(entry.Discovered);
            var row = entry.Memories.Single();
            Assert.IsTrue(row.Unlocked);
            Assert.IsTrue(row.IsGolden);
            Assert.AreEqual("q1", row.LinkedQuestId);
            Assert.AreEqual(QuestState.Awarded, row.LinkedQuestState);
        }

        [Test]
        public void GetJournalEntry_Unknown_ReturnsNull()
        {
            var service = Build(new FakeQuestsService(), new FakeCharactersRepository(), Character("harper"));
            Assert.IsNull(service.GetJournalEntry("nobody"));
        }

        [Test]
        public void GetJournalEntry_Idempotent()
        {
            var quests = new FakeQuestsService().SetState("q1", QuestState.Awarded);
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByQuest("m1", "q1")));

            var a = service.GetJournalEntry("harper");
            var b = service.GetJournalEntry("harper");

            Assert.AreEqual(a.Memories.Single().Unlocked, b.Memories.Single().Unlocked);
            Assert.AreEqual(a.Discovered, b.Discovered);
            Assert.AreEqual(a.Memories.Single().LinkedQuestState, b.Memories.Single().LinkedQuestState);
        }

        [Test]
        public void GetJournalEntry_ChainMemory_LinkDescribesFinalQuest_NotCurrent()
        {
            // Final quest (c2) is Pending while the current quest (c1) is Active — the journal link must
            // describe the award (final) quest consistently, not mix final id with current state.
            var quests = new FakeQuestsService().AddChain("chain",
                new FakeQuest("c1", QuestState.Active, "chain"),
                new FakeQuest("c2", QuestState.Pending, "chain"));
            var service = Build(quests, new FakeCharactersRepository(),
                Character("harper", MemoryByChain("m1", "chain")));

            var row = service.GetJournalEntry("harper").Memories.Single();

            Assert.AreEqual("c2", row.LinkedQuestId);                  // FinalQuest.Id
            Assert.AreEqual(QuestState.Pending, row.LinkedQuestState); // FinalQuest.State (not CurrentQuest's Active)
            Assert.IsFalse(row.Unlocked);
        }
    }
}
