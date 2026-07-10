using System;
using System.Collections.Generic;

namespace Game.Characters.API
{
    /// <summary>
    /// Owns the read-side view of characters: lookup, discovered set, memory state and Journal entries.
    /// A pure projection over CharacterConfig + saved state + IQuestsService — it does not own quest
    /// lifecycle, rewards or conditions (docs/CHARACTER_SYSTEM.md §4).
    ///
    /// Stage 1 is event-free: discovered and memory.Unlocked are derived from quest state on read. The
    /// <see cref="CharacterDiscovered"/> / <see cref="MemoryUnlocked"/> events are declared here but not
    /// raised until Stage 2 wires quest-event subscriptions.
    /// </summary>
    public interface ICharactersService
    {
        /// <summary>The character for <paramref name="characterId"/>, or null if unknown.</summary>
        ICharacter TryGetCharacter(string characterId);

        IEnumerable<ICharacter> GetAllCharacters();
        IEnumerable<ICharacter> GetDiscoveredCharacters();

        bool IsDiscovered(string characterId);
        bool IsMemoryUnlocked(string characterId, string memoryId);

        /// <summary>Flat Journal read model for <paramref name="characterId"/>, or null if unknown.</summary>
        CharacterJournalEntry GetJournalEntry(string characterId);

        /// <summary>A character became discovered. Not raised in Stage 1.</summary>
        event Action<ICharacter> CharacterDiscovered;

        /// <summary>A memory became unlocked. Not raised in Stage 1.</summary>
        event Action<ICharacterMemory> MemoryUnlocked;
    }
}
