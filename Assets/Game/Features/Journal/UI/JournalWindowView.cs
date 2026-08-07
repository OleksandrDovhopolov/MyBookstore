using System;
using System.Collections.Generic;
using Game.Quest.UI;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Journal.UI
{
    /// <summary>Owns tab state and delegates each Journal page to its page view.</summary>
    public sealed class JournalWindowView : WindowView
    {
        // Pixels the content must travel before the scroll counts as "the player moved the list".
        private const float ScrollMoveThresholdPixels = 2f;

        [Header("Tabs")]
        [SerializeField] private JournalTabBar _tabBar;
        [SerializeField] private TextMeshProUGUI _tabTitleLabel;
        [SerializeField] private GameObject[] _tabPages;
        [SerializeField] private ScrollRect[] _tabScrolls;

        [Header("Pages")]
        [SerializeField] private JournalMemoriesPageView _memoriesPage;
        [SerializeField] private JournalPlacesPageView _placesPage;
        [SerializeField] private JournalObjectsPageView _objectsPage;
        [SerializeField] private JournalPeoplePageView _peoplePage;
        [SerializeField] private JournalQuestsPageView _questsPage;
        
        [SerializeField] private QuestRewardWidgetView _rewardInfoWidgetPrefab;

        private JournalTab? _activeTab;
        private Vector2 _lastQuestsContentPosition;

        public event Action<JournalTab> TabSelected;

        /// <summary>Raised when the player drags the quest list, so the reward widget can be dismissed.</summary>
        public event Action QuestsScrolled;

        protected override void Awake()
        {
            base.Awake();
            if (_tabBar != null)
                _tabBar.Selected += OnTabButtonSelected;

            var questsScroll = GetScroll(JournalTab.Quests);
            if (questsScroll != null)
            {
                _lastQuestsContentPosition = GetContentPosition(questsScroll);
                questsScroll.onValueChanged.AddListener(OnQuestsScrollChanged);
            }

            if (_rewardInfoWidgetPrefab != null)
                WidgetRegistry.Register<QuestRewardWidgetData>(_rewardInfoWidgetPrefab);
        }

        public void SelectTab(JournalTab tab)
        {
            if (_activeTab.HasValue && _activeTab.Value != tab)
                ResetScroll(_activeTab.Value);

            // Pages are addressed by index, so _tabPages must stay in JournalTab order. The tab
            // buttons themselves are matched by value inside the bar and may be in any order.
            SetPageActive((int)tab);
            _tabBar?.SelectTab(tab);
            _activeTab = tab;

            if (_tabTitleLabel != null)
                _tabTitleLabel.text = JournalTabTitles.Get(tab);
        }

        public void RenderPeople(IReadOnlyList<JournalCharacterItemModel> models, IUiSpriteProvider sprites)
            => _peoplePage?.Render(models, sprites);

        public void RenderMemories(IReadOnlyList<JournalMemoryItemModel> models, IUiSpriteProvider sprites)
            => _memoriesPage?.Render(models, sprites);

        public void RenderPlaces(IReadOnlyList<JournalPlaceItemModel> models, IUiSpriteProvider sprites)
            => _placesPage?.Render(models, sprites);

        public void RenderObjects(JournalObjectsViewModel model, IUiSpriteProvider sprites, Action<string> onInfoClicked)
            => _objectsPage?.Render(model, sprites, onInfoClicked);

        public void RenderQuests(
            IReadOnlyList<QuestItemModel> models,
            Action<string> onClaim,
            Action<QuestRewardItemModel, RectTransform> onRewardInfo,
            IUiSpriteProvider sprites)
            => _questsPage?.Render(models, onClaim, onRewardInfo, sprites);

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

        private void ResetScroll(JournalTab tab)
        {
            var scroll = GetScroll(tab);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private ScrollRect GetScroll(JournalTab tab)
        {
            var index = (int)tab;
            if (_tabScrolls == null || index < 0 || index >= _tabScrolls.Length) return null;
            return _tabScrolls[index];
        }

        private void OnQuestsScrollChanged(Vector2 _)
        {
            // Content position, not normalizedPosition: the latter degenerates into a constant step
            // when the content fits the viewport, so real drags would produce no delta at all.
            var position = GetContentPosition(GetScroll(JournalTab.Quests));
            if ((position - _lastQuestsContentPosition).sqrMagnitude
                <= ScrollMoveThresholdPixels * ScrollMoveThresholdPixels)
                return;

            _lastQuestsContentPosition = position;
            QuestsScrolled?.Invoke();
        }

        private static Vector2 GetContentPosition(ScrollRect scroll)
        {
            var content = scroll != null ? scroll.content : null;
            return content != null ? content.anchoredPosition : Vector2.zero;
        }

        protected override void OnDestroy()
        {
            if (_tabBar != null)
                _tabBar.Selected -= OnTabButtonSelected;

            var questsScroll = GetScroll(JournalTab.Quests);
            if (questsScroll != null)
                questsScroll.onValueChanged.RemoveListener(OnQuestsScrollChanged);

            base.OnDestroy();
        }
    }
}
