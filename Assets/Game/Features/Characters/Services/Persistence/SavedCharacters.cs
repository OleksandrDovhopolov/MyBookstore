using System.Collections.Generic;

namespace Game.Characters.Services.Persistence
{
    /// <summary>
    /// Persisted character state (save module <see cref="CharactersSaveKeys.State"/>). Stage 1 persists only
    /// the non-derivable <see cref="SavedCharacter.Discovered"/> flag; memory unlock is derived from quest
    /// state at read time. <see cref="SavedCharacter.UnlockedMemoryIds"/> is reserved for Stage 2.
    /// </summary>
    public sealed class SavedCharacters
    {
        public Dictionary<string, SavedCharacter> Characters { get; set; } = new();
    }

    public sealed class SavedCharacter
    {
        public bool Discovered { get; set; }

        /// <summary>Reserved for Stage 2 (event-driven memory persistence). Unused in Stage 1.</summary>
        public HashSet<string> UnlockedMemoryIds { get; set; }
    }
}
