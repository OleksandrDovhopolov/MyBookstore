using System;
using DG.Tweening;
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
        [SerializeField] private ShopTabButton[] _tabButtons = Array.Empty<ShopTabButton>();
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private float _selectedTabYOffset = -50f;
        [SerializeField] private float _tabMoveDuration = 0.18f;
        [SerializeField] private Ease _tabMoveEase = Ease.OutCubic;

        [Header("Offer prefabs")]
        [FormerlySerializedAs("_bookCardsPool")]
        [SerializeField] private UIListPool<ShopItemView> _cardsPool = new();

        private ShopTab? _activeTab;
        private Vector2[] _tabBasePositions;
        private Tween[] _tabMoveTweens;

        public UIListPool<ShopItemView> CardsPool => _cardsPool;

        public event Action<ShopTab> TabSelected;

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

        public void SelectTab(ShopTab tab)
        {
            if (_activeTab.HasValue && _activeTab.Value != tab)
                ResetScroll();

            SetButtonsSelected(tab);
            SetTabVisuals(tab);
            _activeTab = tab;
        }

        private void OnTabButtonSelected(ShopTab tab)
        {
            SelectTab(tab);
            TabSelected?.Invoke(tab);
        }

        private void SetButtonsSelected(ShopTab selectedTab)
        {
            if (_tabButtons == null) return;

            for (var i = 0; i < _tabButtons.Length; i++)
            {
                var button = _tabButtons[i];
                if (button != null) button.SetSelected(button.Tab == selectedTab);
            }
        }

        private void SetTabVisuals(ShopTab selectedTab)
        {
            CacheTabBasePositions();
            if (_tabButtons == null) return;

            for (var i = 0; i < _tabButtons.Length; i++)
            {
                var button = _tabButtons[i];
                var rect = GetTabRect(i);
                if (button == null || rect == null) continue;

                var target = _tabBasePositions[i];
                if (button.Tab == selectedTab)
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

        private void ResetScroll()
        {
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
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
