using System;
using System.Collections.Generic;
using System.Linq;
using Game.Characters.API;
using Game.Journal.UI;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    public sealed class JournalMemoriesViewModelBuilderTests
    {
        [Test]
        public void Build_HidesLockedMemories()
        {
            var models = new JournalMemoriesViewModelBuilder().Build(
                new[] { new CharacterStub("c1") },
                _ => Entry("c1",
                    Memory("locked", unlocked: false, order: 20),
                    Memory("open", unlocked: true, order: 10)));

            CollectionAssert.AreEqual(new[] { "open" }, models.Select(m => m.MemoryId).ToArray());
        }

        [Test]
        public void Build_SortsByOrderDescending_StableForTies()
        {
            var models = new JournalMemoriesViewModelBuilder().Build(
                new[] { new CharacterStub("c1"), new CharacterStub("c2") },
                id => id == "c1"
                    ? Entry("c1", Memory("low", true, 1), Memory("tie_a", true, 5))
                    : Entry("c2", Memory("high", true, 10), Memory("tie_b", true, 5)));

            CollectionAssert.AreEqual(
                new[] { "high", "tie_a", "tie_b", "low" },
                models.Select(m => m.MemoryId).ToArray());
        }

        [Test]
        public void Build_EmptyInput_ReturnsEmpty()
        {
            var models = new JournalMemoriesViewModelBuilder().Build(null, _ => null);
            Assert.AreEqual(0, models.Count);
        }

        [Test]
        public void Build_IncludesUnlockedMemories_FromHiddenCharacters()
        {
            var models = new JournalMemoriesViewModelBuilder().Build(
                new[] { new CharacterStub("owner") },
                _ => new CharacterJournalEntry
                {
                    CharacterId = "owner",
                    HiddenInJournal = true,
                    Memories = new[] { Memory("intro", unlocked: true, order: 0) }
                });

            CollectionAssert.AreEqual(new[] { "intro" }, models.Select(m => m.MemoryId).ToArray());
        }

        private static CharacterJournalEntry Entry(string characterId, params CharacterJournalMemory[] memories)
            => new()
            {
                CharacterId = characterId,
                Memories = memories
            };

        private static CharacterJournalMemory Memory(string id, bool unlocked, int order)
            => new()
            {
                CharacterId = "c",
                MemoryId = id,
                TitleKey = $"title.{id}",
                DescriptionKey = $"description.{id}",
                PhotoKey = $"photo.{id}",
                Unlocked = unlocked,
                Order = order
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
