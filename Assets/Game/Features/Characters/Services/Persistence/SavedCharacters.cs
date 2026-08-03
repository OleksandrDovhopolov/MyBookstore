using System.Collections.Generic;

namespace Game.Characters.Services.Persistence
{
    /// <summary>
    /// Persisted character state (save module <see cref="CharactersSaveKeys.State"/>). Character discovery is
    /// persisted; memory unlock is quest-derived or kept in <see cref="SavedCharacter.UnlockedMemoryIds"/>.
    /// </summary>
    public sealed class SavedCharacters
    {
        public Dictionary<string, SavedCharacter> Characters { get; set; } = new();

        /// <summary>Flat set of unlocked memory ids already seen in the Journal Memories tab.</summary>
        public HashSet<string> SeenMemoryIds { get; set; }
    }

    public sealed class SavedCharacter
    {
        public bool Discovered { get; set; }

        /// <summary>Manual/event ledger that keeps memories unlocked even if quest state later changes.</summary>
        public HashSet<string> UnlockedMemoryIds { get; set; }
    }
}
