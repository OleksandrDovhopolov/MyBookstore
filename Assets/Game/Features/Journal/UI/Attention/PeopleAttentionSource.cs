using System;
using System.Collections.Generic;
using Game.Attention.API;
using Game.Characters.API;

namespace Game.Journal.UI
{
    /// <summary>Ids of every discovered character — the Journal People tab badge.</summary>
    public sealed class PeopleAttentionSource : IAttentionSource, IDisposable
    {
        private readonly ICharactersService _characters;

        public PeopleAttentionSource(ICharactersService characters)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _characters.CharacterDiscovered += OnCharacterChanged;
        }

        public string Key => JournalAttentionKeys.People;

        public IEnumerable<string> CurrentIds
        {
            get
            {
                var characters = _characters.GetDiscoveredCharacters();
                if (characters == null) yield break;

                foreach (var character in characters)
                {
                    if (character == null || string.IsNullOrEmpty(character.Id)) continue;
                    yield return character.Id;
                }
            }
        }

        public event Action Changed;

        public void Dispose() => _characters.CharacterDiscovered -= OnCharacterChanged;

        private void OnCharacterChanged(ICharacter _) => Changed?.Invoke();
    }
}
