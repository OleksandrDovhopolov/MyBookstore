using System.Linq;
using System.Threading;
using Game.Characters.Services;
using Game.Characters.Tests.Editor.Fakes;
using Game.Characters.UI;
using Game.Configs.Models;
using Game.Quest.API;
using NUnit.Framework;

namespace Game.Characters.Tests.Editor
{
    public sealed class JournalCharactersViewModelBuilderTests
    {
        private static CharacterConfig Character(string id, string portraitKey, params CharacterMemoryConfig[] memories)
            => new()
            {
                Id = id,
                DisplayNameKey = $"character.{id}.name",
                RoleKey = $"character.{id}.role",
                PortraitKey = portraitKey,
                Memories = memories,
            };

        private static CharacterMemoryConfig MemoryByQuest(string id, string questId, bool golden = false)
            => new() { Id = id, QuestId = questId, IsGolden = golden, TitleKey = $"m.{id}.t" };

        private static CharactersService Load(FakeQuestsService quests, params CharacterConfig[] configs)
        {
            var save = new FakeSaveService();
            var cfgs = new FakeConfigsService();
            foreach (var c in configs) cfgs.Add(c);

            var svc = new CharactersService(save, cfgs, quests, new FakeCharactersRepository());
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return svc;
        }

        [Test]
        public void Build_ShowsAllCharacters_IncludingUndiscovered()
        {
            var quests = new FakeQuestsService().SetState("harper_q", QuestState.Awarded);
            var svc = Load(quests,
                Character("harper", "portrait_harper", MemoryByQuest("m1", "harper_q", golden: true)),
                Character("walt", "portrait_walt", MemoryByQuest("w1", "walt_q")));

            var models = new JournalCharactersViewModelBuilder().Build(svc.GetAllCharacters(), svc.GetJournalEntry);

            CollectionAssert.AreEquivalent(
                new[] { "harper", "walt" }, models.Select(m => m.CharacterId).ToArray());
        }

        [Test]
        public void Build_DiscoveredCharacter_HasPortraitAndMemoryState()
        {
            var quests = new FakeQuestsService().SetState("harper_q", QuestState.Awarded);
            var svc = Load(quests,
                Character("harper", "portrait_harper", MemoryByQuest("m1", "harper_q", golden: true)));

            var model = new JournalCharactersViewModelBuilder()
                .Build(svc.GetAllCharacters(), svc.GetJournalEntry).Single();

            Assert.IsTrue(model.IsDiscovered);
            Assert.IsFalse(model.Locked);
            Assert.AreEqual("portrait_harper", model.PortraitKey);
            Assert.AreEqual(1, model.TotalMemoryCount);
            Assert.AreEqual(1, model.UnlockedMemoryCount);

            var memory = model.Memories.Single();
            Assert.IsTrue(memory.IsUnlocked);
            Assert.IsTrue(memory.IsGolden);
            Assert.AreEqual(QuestState.Awarded, memory.LinkedQuestState);
        }

        [Test]
        public void Build_UndiscoveredCharacter_IsLocked_WithZeroUnlocked()
        {
            var quests = new FakeQuestsService(); // walt_q stays Pending
            var svc = Load(quests, Character("walt", "portrait_walt", MemoryByQuest("w1", "walt_q")));

            var model = new JournalCharactersViewModelBuilder()
                .Build(svc.GetAllCharacters(), svc.GetJournalEntry).Single();

            Assert.IsFalse(model.IsDiscovered);
            Assert.IsTrue(model.Locked);
            Assert.AreEqual(1, model.TotalMemoryCount);
            Assert.AreEqual(0, model.UnlockedMemoryCount);
            Assert.IsFalse(model.Memories.Single().IsUnlocked);
        }
    }
}
