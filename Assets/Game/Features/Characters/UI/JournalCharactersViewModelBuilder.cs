using System;
using System.Collections.Generic;
using Game.Characters.API;

namespace Game.Characters.UI
{
    /// <summary>
    /// Pure mapper: characters + their journal entries → flat row view-models for the Journal Characters
    /// view. No Unity / service dependency, so it is unit-testable. Shows every character (discovered and
    /// undiscovered); the row view decides how to render <see cref="JournalCharacterItemModel.Locked"/>.
    /// </summary>
    public sealed class JournalCharactersViewModelBuilder
    {
        public IReadOnlyList<JournalCharacterItemModel> Build(
            IEnumerable<ICharacter> characters, Func<string, CharacterJournalEntry> getEntry)
        {
            var result = new List<JournalCharacterItemModel>();
            if (characters == null || getEntry == null) return result;

            foreach (var character in characters)
            {
                if (character?.Id == null) continue;
                var entry = getEntry(character.Id);
                if (entry == null) continue;

                result.Add(BuildOne(entry));
            }

            return result;
        }

        private static JournalCharacterItemModel BuildOne(CharacterJournalEntry entry)
        {
            var memoryConfigs = entry.Memories ?? Array.Empty<CharacterJournalMemory>();
            var memories = new JournalMemoryItemModel[memoryConfigs.Length];
            var unlockedCount = 0;

            for (var i = 0; i < memoryConfigs.Length; i++)
            {
                var m = memoryConfigs[i];
                if (m.Unlocked) unlockedCount++;
                memories[i] = new JournalMemoryItemModel(
                    m.MemoryId, m.TitleKey, m.DescriptionKey, m.Unlocked, m.IsGolden, m.LinkedQuestState);
            }

            return new JournalCharacterItemModel(
                entry.CharacterId, entry.DisplayNameKey, entry.RoleKey, entry.PortraitKey,
                entry.Discovered, unlockedCount, memoryConfigs.Length, memories);
        }
    }
}
