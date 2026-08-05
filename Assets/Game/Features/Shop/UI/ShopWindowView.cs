using System;
using Game.UI;
using UIShared;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Shop.UI
{
    public sealed class ShopWindowView : WindowView
    {
        [Header("Tabs")]
        [SerializeField] private TabBar _tabBar;
        [SerializeField] private ScrollRect _scroll;

        [Header("Offer prefabs")]
        [FormerlySerializedAs("_bookCardsPool")]
        [SerializeField] private UIListPool<ShopItemView> _cardsPool = new();

        private TabType? _activeTab;

        public UIListPool<ShopItemView> CardsPool => _cardsPool;

        public event Action<TabType> TabSelected;

        protected override void Awake()
        {
            base.Awake();
            if (_tabBar != null)
                _tabBar.Selected += OnTabButtonSelected;
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

        protected override void OnDestroy()
        {
            if (_tabBar != null)
                _tabBar.Selected -= OnTabButtonSelected;

            base.OnDestroy();
        }
    }
}
