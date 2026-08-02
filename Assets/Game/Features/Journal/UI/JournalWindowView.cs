using System;
using System.Collections.Generic;
using Game.UI;
using SpriteService;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Journal.UI
{
    /// <summary>Owns tab state and delegates each Journal page to its page view.</summary>
    public sealed class JournalWindowView : WindowView
    {
        [Header("Tabs")]
        [SerializeField] private JournalTabButton[] _tabButtons;
        [SerializeField] private GameObject[] _tabPages;
        [SerializeField] private ScrollRect[] _tabScrolls;

        [Header("Pages")]
        [SerializeField] private JournalMemoriesPageView _memoriesPage;
        [SerializeField] private JournalPlacesPageView _placesPage;
        [SerializeField] private JournalObjectsPageView _objectsPage;
        [SerializeField] private JournalPeoplePageView _peoplePage;

        private JournalTab? _activeTab;

        public event Action<JournalTab> TabSelected;

        protected override void Awake()
        {
            base.Awake();
            if (_tabButtons == null) return;
            for (var i = 0; i < _tabButtons.Length; i++)
            {
                var button = _tabButtons[i];
                if (button != null) button.Selected += OnTabButtonSelected;
            }
        }

        public void SelectTab(JournalTab tab)
        {
            if (_activeTab.HasValue && _activeTab.Value != tab)
                ResetScroll(_activeTab.Value);

            var selectedIndex = (int)tab;
            SetPageActive(selectedIndex);
            SetButtonsSelected(selectedIndex);
            _activeTab = tab;
        }

        public void RenderPeople(IReadOnlyList<JournalCharacterItemModel> models, IUiSpriteProvider sprites)
            => _peoplePage?.Render(models, sprites);

        public void RenderMemories(IReadOnlyList<JournalMemoryItemModel> models, IUiSpriteProvider sprites)
            => _memoriesPage?.Render(models, sprites);

        public void RenderPlaces(IReadOnlyList<JournalPlaceItemModel> models, IUiSpriteProvider sprites)
            => _placesPage?.Render(models, sprites);

        public void RenderObjects(JournalObjectsViewModel model, IUiSpriteProvider sprites)
            => _objectsPage?.Render(model, sprites);

        public void RenderQuestsEmpty()
        {
        }

        public void Clear()
        {
            _peoplePage?.Clear();
            _memoriesPage?.Clear();
            _placesPage?.Clear();
            _objectsPage?.Clear();
        }

        private void OnTabButtonSelected(JournalTab tab)
        {
            SelectTab(tab);
            TabSelected?.Invoke(tab);
        }

        private void SetPageActive(int selectedIndex)
        {
            if (_tabPages == null) return;
            for (var i = 0; i < _tabPages.Length; i++)
            {
                if (_tabPages[i] != null)
                    _tabPages[i].SetActive(i == selectedIndex);
            }
        }

        private void SetButtonsSelected(int selectedIndex)
        {
            if (_tabButtons == null) return;
            for (var i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                    _tabButtons[i].SetSelected(i == selectedIndex);
            }
        }

        private void ResetScroll(JournalTab tab)
        {
            var index = (int)tab;
            if (_tabScrolls == null || index < 0 || index >= _tabScrolls.Length) return;
            var scroll = _tabScrolls[index];
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnDestroy()
        {
            if (_tabButtons != null)
            {
                for (var i = 0; i < _tabButtons.Length; i++)
                {
                    var button = _tabButtons[i];
                    if (button != null) button.Selected -= OnTabButtonSelected;
                }
            }

            base.OnDestroy();
        }
    }
}
