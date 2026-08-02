using System.Linq;
using System.Threading;
using Game.Characters.Services;
using Game.Characters.Tests.Editor.Fakes;
using Game.Configs.Models;
using Game.Quest.API;
using NUnit.Framework;

namespace Game.Characters.Tests.Editor
{
    public sealed class CharacterJournalEntryProjectionTests
    {
        [Test]
        public void GetJournalEntry_ProjectsFavoriteGenresAndMemoryFields()
        {
            var configs = new FakeConfigsService();
            configs.Add(new CharacterConfig
            {
                Id = "harper",
                DisplayNameKey = "character.harper.name",
                PortraitKey = "portrait_harper",
                FavoriteGenres = new[] { "Fact", "Travel" },
                Memories = new[]
                {
                    new CharacterMemoryConfig
                    {
                        Id = "m1",
                        QuestId = "q1",
                        TitleKey = "memory.title",
                        DescriptionKey = "memory.description",
                        PhotoKey = "memory_photo",
                        Order = 7,
                        IsGolden = true
                    }
                }
            });
            var quests = new FakeQuestsService().SetState("q1", QuestState.Awarded);
            var service = new CharactersService(
                new FakeSaveService(),
                configs,
                quests,
                new FakeCharactersRepository());
            service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            var entry = service.GetJournalEntry("harper");

            CollectionAssert.AreEqual(new[] { "Fact", "Travel" }, entry.FavoriteGenres);
            var memory = entry.Memories.Single();
            Assert.AreEqual("harper", memory.CharacterId);
            Assert.AreEqual("memory_photo", memory.PhotoKey);
            Assert.AreEqual(7, memory.Order);
            Assert.IsTrue(memory.IsGolden);
            Assert.IsTrue(memory.Unlocked);
        }
    }
}
