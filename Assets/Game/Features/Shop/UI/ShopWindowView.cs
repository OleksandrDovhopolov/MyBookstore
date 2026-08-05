using System;
using Game.UI;
using Game.UI.ContentWidget;
using UIShared;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Shop.UI
{
    public sealed class ShopWindowView : WindowView
    {
        private const float ScrollMoveThresholdPixels = 2f;

        [Header("Tabs")]
        [SerializeField] private TabBar _tabBar;
        [SerializeField] private ScrollRect _scroll;

        [Header("Offer prefabs")]
        [FormerlySerializedAs("_bookCardsPool")]
        [SerializeField] private UIListPool<ShopItemView> _cardsPool = new();
        [SerializeField] private ShopItemWidgetView _itemInfoWidgetPrefab;

        private TabType? _activeTab;
        private Vector2 _lastContentPosition;

        public UIListPool<ShopItemView> CardsPool => _cardsPool;

        public event Action<TabType> TabSelected;
        public event Action Scrolled;

        protected override void Awake()
        {
            base.Awake();
            if (_tabBar != null)
                _tabBar.Selected += OnTabButtonSelected;

            if (_scroll != null)
            {
                _lastContentPosition = GetContentPosition();
                _scroll.onValueChanged.AddListener(OnScrollChanged);
            }

            if (_itemInfoWidgetPrefab != null)
                WidgetRegistry.Register<ShopItemWidgetData>(_itemInfoWidgetPrefab);
        }

        public void SelectTab(TabType tab)
        {
            if (_activeTab.HasValue && _activeTab.Value != tab)
                ResetScroll();

            _tabBar?.SelectTab(tab);
            _activeTab = tab;
        }

        private void OnTabButtonSelected(TabType tab)
        {
            SelectTab(tab);
            TabSelected?.Invoke(tab);
        }

        private void ResetScroll()
        {
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        private void OnScrollChanged(Vector2 _)
        {
            var position = GetContentPosition();
            if ((position - _lastContentPosition).sqrMagnitude
                <= ScrollMoveThresholdPixels * ScrollMoveThresholdPixels)
                return;

            _lastContentPosition = position;
            Scrolled?.Invoke();
        }

        private Vector2 GetContentPosition()
        {
            var content = _scroll != null ? _scroll.content : null;
            return content != null ? content.anchoredPosition : Vector2.zero;
        }

        protected override void OnDestroy()
        {
            if (_tabBar != null)
                _tabBar.Selected -= OnTabButtonSelected;
            if (_scroll != null)
                _scroll.onValueChanged.RemoveListener(OnScrollChanged);

            base.OnDestroy();
        }
    }
}
