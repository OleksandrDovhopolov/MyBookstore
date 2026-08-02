using System;
using System.Collections.Generic;
using System.Linq;
using Game.Characters.API;
using Game.Journal.UI;
using Game.Quest.API;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    public sealed class JournalCharactersViewModelBuilderTests
    {
        [Test]
        public void Build_ShowsAllCharacters_IncludingUndiscovered()
        {
            var characters = new[]
            {
                new CharacterStub("harper"),
                new CharacterStub("walt")
            };

            var models = new JournalCharactersViewModelBuilder().Build(characters, Entry);

            CollectionAssert.AreEquivalent(
                new[] { "harper", "walt" },
                models.Select(m => m.CharacterId).ToArray());
        }

        [Test]
        public void Build_DiscoveredCharacter_HasPortraitMemoryStateAndGenres()
        {
            var models = new JournalCharactersViewModelBuilder().Build(
                new[] { new CharacterStub("harper") },
                _ => Entry("harper", discovered: true, favoriteGenres: new[] { "Fact", "Travel" },
                    memories: new[]
                    {
                        new CharacterJournalMemory
                        {
                            CharacterId = "harper",
                            MemoryId = "m1",
                            TitleKey = "m.m1.t",
                            DescriptionKey = "m.m1.d",
                            PhotoKey = "photo_m1",
                            Order = 10,
                            Unlocked = true,
                            IsGolden = true,
                            LinkedQuestState = QuestState.Awarded
                        }
                    }));

            var model = models.Single();
            Assert.IsTrue(model.IsDiscovered);
            Assert.IsFalse(model.Locked);
            Assert.AreEqual("portrait_harper", model.PortraitKey);
            CollectionAssert.AreEqual(new[] { "Fact", "Travel" }, model.FavoriteGenres);
            Assert.AreEqual(1, model.TotalMemoryCount);
            Assert.AreEqual(1, model.UnlockedMemoryCount);

            var memory = model.Memories.Single();
            Assert.AreEqual("photo_m1", memory.PhotoKey);
            Assert.AreEqual(10, memory.Order);
            Assert.IsTrue(memory.IsUnlocked);
            Assert.IsTrue(memory.IsGolden);
            Assert.AreEqual(QuestState.Awarded, memory.LinkedQuestState);
        }

        [Test]
        public void Build_UndiscoveredCharacter_IsLocked_WithZeroUnlocked()
        {
            var model = new JournalCharactersViewModelBuilder()
                .Build(
                    new[] { new CharacterStub("walt") },
                    _ => Entry("walt", memories: new[]
                    {
                        new CharacterJournalMemory { CharacterId = "walt", MemoryId = "w1", Unlocked = false }
                    }))
                .Single();

            Assert.IsFalse(model.IsDiscovered);
            Assert.IsTrue(model.Locked);
            Assert.AreEqual(1, model.TotalMemoryCount);
            Assert.AreEqual(0, model.UnlockedMemoryCount);
            Assert.IsFalse(model.Memories.Single().IsUnlocked);
        }

        private static CharacterJournalEntry Entry(string id)
            => Entry(id, discovered: id == "harper");

        private static CharacterJournalEntry Entry(
            string id,
            bool discovered = false,
            string[] favoriteGenres = null,
            CharacterJournalMemory[] memories = null)
            => new()
            {
                CharacterId = id,
                Discovered = discovered,
                DisplayNameKey = $"character.{id}.name",
                RoleKey = $"character.{id}.role",
                PortraitKey = $"portrait_{id}",
                FavoriteGenres = favoriteGenres ?? Array.Empty<string>(),
                Memories = memories ?? Array.Empty<CharacterJournalMemory>()
            };

        private sealed class CharacterStub : ICharacter
        {
            public CharacterStub(string id) => Id = id;

            public string Id { get; }
            public bool Discovered => true;
            public Game.Configs.Models.CharacterConfig Config => null;
            public IReadOnlyList<ICharacterMemory> Memories => Array.Empty<ICharacterMemory>();
        }
    }
}
