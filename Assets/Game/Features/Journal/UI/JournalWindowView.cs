using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Quest.UI;
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
        [SerializeField] private float _selectedTabYOffset = -50f;
        [SerializeField] private float _tabMoveDuration = 0.18f;
        [SerializeField] private Ease _tabMoveEase = Ease.OutCubic;

        [Header("Pages")]
        [SerializeField] private JournalMemoriesPageView _memoriesPage;
        [SerializeField] private JournalPlacesPageView _placesPage;
        [SerializeField] private JournalObjectsPageView _objectsPage;
        [SerializeField] private JournalPeoplePageView _peoplePage;
        [SerializeField] private JournalQuestsPageView _questsPage;

        private JournalTab? _activeTab;
        private Vector2[] _tabBasePositions;
        private Tween[] _tabMoveTweens;

        public event Action<JournalTab> TabSelected;

        protected override void Awake()
        {
            base.Awake();
            CacheTabBasePositions();
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
            SetTabVisuals(selectedIndex);
            _activeTab = tab;
        }

        public void RenderPeople(IReadOnlyList<JournalCharacterItemModel> models, IUiSpriteProvider sprites)
            => _peoplePage?.Render(models, sprites);

        public void RenderMemories(IReadOnlyList<JournalMemoryItemModel> models, IUiSpriteProvider sprites)
            => _memoriesPage?.Render(models, sprites);

        public void RenderPlaces(IReadOnlyList<JournalPlaceItemModel> models, IUiSpriteProvider sprites)
            => _placesPage?.Render(models, sprites);

        public void RenderObjects(JournalObjectsViewModel model, IUiSpriteProvider sprites, Action<string> onInfoClicked)
            => _objectsPage?.Render(model, sprites, onInfoClicked);

        public void RenderQuests(IReadOnlyList<QuestItemModel> models, Action<string> onClaim, IUiSpriteProvider sprites)
            => _questsPage?.Render(models, onClaim, sprites);

        public void Clear()
        {
            _peoplePage?.Clear();
            _memoriesPage?.Clear();
            _placesPage?.Clear();
            _objectsPage?.Clear();
            _questsPage?.Clear();
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

        private void SetTabVisuals(int selectedIndex)
        {
            CacheTabBasePositions();
            if (_tabButtons == null) return;

            for (var i = 0; i < _tabButtons.Length; i++)
            {
                var rect = GetTabRect(i);
                if (rect == null) continue;

                var target = _tabBasePositions[i];
                if (i == selectedIndex)
                    target.y += _selectedTabYOffset;

                MoveTab(i, rect, target);
            }
        }

        private void CacheTabBasePositions()
        {
            var count = _tabButtons?.Length ?? 0;
            if (count == 0) return;
            if (_tabBasePositions != null && _tabBasePositions.Length == count) return;

            _tabBasePositions = new Vector2[count];
            _tabMoveTweens = new Tween[count];
            for (var i = 0; i < count; i++)
            {
                var rect = GetTabRect(i);
                _tabBasePositions[i] = rect != null ? rect.anchoredPosition : Vector2.zero;
            }
        }

        private void MoveTab(int index, RectTransform rect, Vector2 target)
        {
            if (_tabMoveTweens != null && index >= 0 && index < _tabMoveTweens.Length)
            {
                _tabMoveTweens[index]?.Kill();
                _tabMoveTweens[index] = null;
            }

            if (_tabMoveDuration <= 0f)
            {
                rect.anchoredPosition = target;
                return;
            }

            var tween = DOTween
                .To(() => rect.anchoredPosition, value => rect.anchoredPosition = value, target, _tabMoveDuration)
                .SetEase(_tabMoveEase)
                .SetUpdate(true);

            if (_tabMoveTweens != null && index >= 0 && index < _tabMoveTweens.Length)
                _tabMoveTweens[index] = tween;
        }

        private RectTransform GetTabRect(int index)
        {
            if (_tabButtons == null || index < 0 || index >= _tabButtons.Length) return null;
            return _tabButtons[index] != null
                ? _tabButtons[index].GetComponent<RectTransform>()
                : null;
        }

        private void KillTabTweens()
        {
            if (_tabMoveTweens == null) return;
            for (var i = 0; i < _tabMoveTweens.Length; i++)
            {
                _tabMoveTweens[i]?.Kill();
                _tabMoveTweens[i] = null;
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
            KillTabTweens();
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
