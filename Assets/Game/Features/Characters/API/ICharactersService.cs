using System;
using System.Collections.Generic;

namespace Game.Characters.API
{
    /// <summary>
    /// Owns the read-side view of characters: lookup, discovered set, memory state and Journal entries.
    /// A pure projection over CharacterConfig + saved state + IQuestsService — it does not own quest
    /// lifecycle, rewards or conditions (docs/CHARACTER_SYSTEM.md §4).
    ///
    /// Discovered and memory.Unlocked are derived from quest state plus the persisted manual-unlock ledger.
    /// Quest lifecycle events are mirrored as character/memory events when they change the read model.
    /// </summary>
    public interface ICharactersService
    {
        /// <summary>The character for <paramref name="characterId"/>, or null if unknown.</summary>
        ICharacter TryGetCharacter(string characterId);

        IEnumerable<ICharacter> GetAllCharacters();
        IEnumerable<ICharacter> GetDiscoveredCharacters();

        bool IsDiscovered(string characterId);
        bool IsMemoryUnlocked(string characterId, string memoryId);

        /// <summary>Manual unlock for FTUE/tutorial/content scripts. True only when this call unlocks a new memory.</summary>
        bool TryUnlockMemory(string characterId, string memoryId);

        int UnseenMemoryCount { get; }
        bool HasUnseenMemories { get; }

        /// <summary>Marks every currently unlocked memory as seen.</summary>
        void MarkAllMemoriesSeen();

        /// <summary>Flat Journal read model for <paramref name="characterId"/>, or null if unknown.</summary>
        CharacterJournalEntry GetJournalEntry(string characterId);

        /// <summary>A character became discovered.</summary>
        event Action<ICharacter> CharacterDiscovered;

        /// <summary>A memory became unlocked.</summary>
        event Action<ICharacterMemory> MemoryUnlocked;

        event Action UnseenMemoriesChanged;
    }
}
