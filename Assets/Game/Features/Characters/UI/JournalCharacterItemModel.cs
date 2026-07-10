using System.Collections.Generic;

namespace Game.Characters.UI
{
    /// <summary>
    /// Immutable row data for one character in the Journal Characters view. We show every character;
    /// <see cref="Locked"/> ones render a placeholder instead of the portrait (decided in the row view).
    /// </summary>
    public sealed class JournalCharacterItemModel
    {
        public JournalCharacterItemModel(string characterId, string displayNameKey, string roleKey,
            string portraitKey, bool isDiscovered, int unlockedMemoryCount, int totalMemoryCount,
            IReadOnlyList<JournalMemoryItemModel> memories)
        {
            CharacterId = characterId;
            DisplayNameKey = displayNameKey;
            RoleKey = roleKey;
            PortraitKey = portraitKey;
            IsDiscovered = isDiscovered;
            UnlockedMemoryCount = unlockedMemoryCount;
            TotalMemoryCount = totalMemoryCount;
            Memories = memories ?? System.Array.Empty<JournalMemoryItemModel>();
        }

        public string CharacterId { get; }
        public string DisplayNameKey { get; }
        public string RoleKey { get; }
        public string PortraitKey { get; }
        public bool IsDiscovered { get; }

        /// <summary>Convenience for the row view: true when the character is not yet discovered.</summary>
        public bool Locked => !IsDiscovered;

        public int UnlockedMemoryCount { get; }
        public int TotalMemoryCount { get; }
        public IReadOnlyList<JournalMemoryItemModel> Memories { get; }
    }
}
