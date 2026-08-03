using System;
using Cysharp.Threading.Tasks;
using Game.Characters.API;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Decor.UI;
using Game.LocationUnlock.API;
using Game.UI;
using SpriteService;
using VContainer;

namespace Game.Journal.UI
{
    /// <summary>
    /// Journal window with Memories, Places, Objects, People, and Quests tabs.
    /// Opened via <c>uiManager.ShowAsync&lt;JournalWindow&gt;()</c>; needs a prefab at address "JournalWindow".
    /// </summary>
    [Window("JournalWindow", WindowType.Page, true)]
    public sealed class JournalWindow : WindowController<JournalWindowView>
    {
        private readonly JournalCharactersViewModelBuilder _peopleBuilder = new();
        private readonly JournalMemoriesViewModelBuilder _memoriesBuilder = new();
        private readonly JournalPlacesViewModelBuilder _placesBuilder = new();
        private readonly JournalObjectsViewModelBuilder _objectsBuilder = new();

        private JournalTab _activeTab = JournalTab.People;
        private ICharactersService _characters;
        private IConfigsService _configs;
        private ILocationUnlockService _locations;
        private IDecorPlacementService _decorPlacement;
        private IDecorTotalEffectsProvider _decorEffects;
        private IUiSpriteProvider _sprites;

        [Inject]
        public void InjectServices(
            ICharactersService characters = null,
            IConfigsService configs = null,
            ILocationUnlockService locations = null,
            IDecorPlacementService decorPlacement = null,
            IDecorTotalEffectsProvider decorEffects = null,
            IUiSpriteProvider sprites = null)
        {
            _characters = characters;
            _configs = configs;
            _locations = locations;
            _decorPlacement = decorPlacement;
            _decorEffects = decorEffects;
            _sprites = sprites;
        }

        protected override void OnInit()
        {
        }

        protected override void OnShowStart()
        {
            View.TabSelected += OnTabSelected;

            if (_characters != null)
            {
                _characters.CharacterDiscovered += OnCharacterDiscovered;
                _characters.MemoryUnlocked += OnMemoryUnlocked;
            }

            if (_locations != null)
            {
                _locations.Unlocked += OnLocationChanged;
                _locations.StatusChanged += OnLocationChanged;
            }

            if (_decorPlacement != null)
                _decorPlacement.PlacementChanged += OnDecorPlacementChanged;

            RenderAll();
            View.SelectTab(_activeTab);
            MarkMemoriesSeenIfActive();
        }

        protected override void OnHideStart(bool isClosed)
        {
            View.TabSelected -= OnTabSelected;

            if (_characters != null)
            {
                _characters.CharacterDiscovered -= OnCharacterDiscovered;
                _characters.MemoryUnlocked -= OnMemoryUnlocked;
            }

            if (_locations != null)
            {
                _locations.Unlocked -= OnLocationChanged;
                _locations.StatusChanged -= OnLocationChanged;
            }

            if (_decorPlacement != null)
                _decorPlacement.PlacementChanged -= OnDecorPlacementChanged;
        }

        protected override void OnDispose() => View.Clear();

        private void OnTabSelected(JournalTab tab)
        {
            _activeTab = tab;
            MarkMemoriesSeenIfActive();
        }

        private void OnCharacterDiscovered(ICharacter _)
        {
            RenderPeople();
            RenderMemories();
        }

        private void OnMemoryUnlocked(ICharacterMemory _)
        {
            RenderPeople();
            RenderMemories();
        }

        private void OnLocationChanged(string _) => RenderPlaces();

        private void OnDecorPlacementChanged() => RenderObjects();

        private void RenderAll()
        {
            RenderPeople();
            RenderMemories();
            RenderPlaces();
            RenderObjects();
            View.RenderQuestsEmpty();
        }

        private void RenderPeople()
        {
            if (_characters == null)
            {
                View.RenderPeople(Array.Empty<JournalCharacterItemModel>(), _sprites);
                return;
            }

            var models = _peopleBuilder.Build(_characters.GetAllCharacters(), _characters.GetJournalEntry);
            View.RenderPeople(models, _sprites);
        }

        private void RenderMemories()
        {
            if (_characters == null)
            {
                View.RenderMemories(Array.Empty<JournalMemoryItemModel>(), _sprites);
                return;
            }

            var models = _memoriesBuilder.Build(_characters.GetAllCharacters(), _characters.GetJournalEntry);
            View.RenderMemories(models, _sprites);
        }

        private void RenderPlaces()
        {
            var locations = _configs != null
                ? _configs.GetAll<LocationConfig>()
                : Array.Empty<LocationConfig>();
            var models = _placesBuilder.Build(locations, IsLocationUnlocked);
            View.RenderPlaces(models, _sprites);
        }

        private void RenderObjects()
        {
            var activeDecorIds = _decorPlacement?.GetActiveDecorIds() ?? Array.Empty<string>();
            var models = _objectsBuilder.Build(activeDecorIds, _configs, _decorEffects);
            View.RenderObjects(models, _sprites, OnDecorInfoClicked);
        }

        private bool IsLocationUnlocked(string locationId)
            => _locations == null
               || _locations.GetStatus(locationId)?.State == LocationUnlockState.Unlocked;

        private void MarkMemoriesSeenIfActive()
        {
            if (_activeTab != JournalTab.Memories) return;
            _characters?.MarkAllMemoriesSeen();
        }

        private void OnDecorInfoClicked(string decorId)
        {
            if (string.IsNullOrEmpty(decorId)) return;

            UIManager.ShowAsync<DecorInfoPopup>(
                new DecorInfoPopupArgs(decorId),
                View != null ? View.destroyCancellationToken : default).Forget();
        }
    }
}
