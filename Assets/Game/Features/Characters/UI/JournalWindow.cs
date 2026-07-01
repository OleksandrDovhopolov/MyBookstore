using Game.Characters.API;
using Game.Newspaper.UI;
using Game.UI;
using VContainer;

namespace Game.Characters.UI
{
    /// <summary>
    /// Journal — Characters section. Lists every character; undiscovered ones render a placeholder
    /// (see <see cref="JournalCharacterRowView"/>). Live-refreshes on discovery / memory-unlock events
    /// (Stage 2). Mirrors <see cref="Game.Location.UI.LocationWindow"/>. Opened via
    /// <c>uiManager.ShowAsync&lt;JournalWindow&gt;()</c>; needs a prefab at address "JournalWindow".
    /// </summary>
    [Window("JournalWindow", WindowType.Page)]
    public sealed class JournalWindow : WindowController<JournalWindowView>
    {
        private readonly JournalCharactersViewModelBuilder _builder = new();

        private ICharactersService _characters;
        private IUiSpriteProvider _sprites;

        [Inject]
        public void InjectServices(ICharactersService characters, IUiSpriteProvider sprites = null)
        {
            _characters = characters;
            _sprites = sprites;
        }

        protected override void OnInit()
        {
        }

        protected override void OnShowStart()
        {
            if (_characters != null)
            {
                _characters.CharacterDiscovered += OnCharacterDiscovered;
                _characters.MemoryUnlocked += OnMemoryUnlocked;
            }

            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_characters != null)
            {
                _characters.CharacterDiscovered -= OnCharacterDiscovered;
                _characters.MemoryUnlocked -= OnMemoryUnlocked;
            }
        }

        protected override void OnDispose() => View.Clear();

        private void OnCharacterDiscovered(ICharacter _) => Render();
        private void OnMemoryUnlocked(ICharacterMemory _) => Render();

        private void Render()
        {
            if (_characters == null)
            {
                View.Clear();
                return;
            }

            var models = _builder.Build(_characters.GetAllCharacters(), _characters.GetJournalEntry);
            View.Render(models, _sprites);
        }
    }
}
