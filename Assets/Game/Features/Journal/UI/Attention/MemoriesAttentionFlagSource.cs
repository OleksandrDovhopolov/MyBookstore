using System;
using Game.Attention.API;
using Game.Characters.API;

namespace Game.Journal.UI
{
    /// <summary>
    /// Memories are the one Journal category whose seen-state is <b>not</b> stored in the attention
    /// save module: <c>CharactersService</c> owns <c>SavedCharacters.SeenMemoryIds</c> plus its own
    /// dirty flag. Exposing it as an <see cref="IAttentionFlagSource"/> keeps that ownership intact
    /// while still surfacing the badge through the shared service.
    /// </summary>
    public sealed class MemoriesAttentionFlagSource : IAttentionFlagSource, IDisposable
    {
        private readonly ICharactersService _characters;

        public MemoriesAttentionFlagSource(ICharactersService characters)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _characters.MemoryUnlocked += OnMemoryChanged;
            _characters.UnseenMemoriesChanged += OnUnseenMemoriesChanged;
        }

        public string Key => JournalAttentionKeys.Memories;

        public bool HasUnseen => _characters.HasUnseenMemories;

        public event Action Changed;

        public void MarkSeen() => _characters.MarkAllMemoriesSeen();

        public void Dispose()
        {
            _characters.MemoryUnlocked -= OnMemoryChanged;
            _characters.UnseenMemoriesChanged -= OnUnseenMemoriesChanged;
        }

        private void OnMemoryChanged(ICharacterMemory _) => Changed?.Invoke();
        private void OnUnseenMemoriesChanged() => Changed?.Invoke();
    }
}
