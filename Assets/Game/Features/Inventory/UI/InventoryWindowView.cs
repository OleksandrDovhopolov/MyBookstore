using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Decor;
using Game.Inventory.API;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory.UI
{
    public class InventoryWindowView : WindowView
    {
        // Pixels the content must travel before the scroll counts as "the player moved the list".
        private const float ScrollMoveThresholdPixels = 2f;

        [Header("List")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private TabBar _tabBar;
        [SerializeField] private UIListPool<InventoryItemRowView> _rowPool = new();
        [SerializeField] private InventoryItemWidgetView _itemInfoWidgetPrefab;

        private IInventoryService _inventory;
        private IUiSpriteProvider _sprites;
        private IReadOnlyList<IInventoryRowSource> _rowSources;
        private IDecorPlacementService _decorPlacement;
        private readonly List<RowEntry> _rows = new();
        private Action<string, InventoryRowStyle, RectTransform> _onRowInfo;
        private Action _onRowsRebuilt;
        private Vector2 _lastContentPosition;
        private TabType _activeTab = TabType.All;

        private readonly CancellationTokenSource _cts = new();
        private CancellationTokenSource _renderCts;
        private bool _isBound;

        protected override void Awake()
        {
            base.Awake();
            if (_tabBar != null)
                _tabBar.Selected += OnTabSelected;

            if (_itemInfoWidgetPrefab != null)
                WidgetRegistry.Register<InventoryItemWidgetData>(_itemInfoWidgetPrefab);
        }

        /// <summary>
        /// Called once by the controller in OnInit. Stores deps and subscribes to inventory
        /// changes. Subsequent re-shows go through <see cref="Refresh"/>.
        /// </summary>
        public void Bind(
            IInventoryService inventory,
            IUiSpriteProvider sprites,
            IReadOnlyList<IInventoryRowSource> rowSources,
            IDecorPlacementService decorPlacement,
            Action<string, InventoryRowStyle, RectTransform> onRowInfo,
            Action onRowsRebuilt)
        {
            if (_isBound) return;

            _inventory = inventory;
            _sprites = sprites;
            _rowSources = rowSources != null
                ? rowSources.OrderBy(source => source.Order).ToList()
                : Array.Empty<IInventoryRowSource>();
            _decorPlacement = decorPlacement;
            _onRowInfo = onRowInfo;
            _onRowsRebuilt = onRowsRebuilt;

            if (_inventory == null)
            {
                Debug.LogWarning("[InventoryWindowView] dependencies missing - not registered in DI?");
                return;
            }

            _inventory.Changed += OnInventoryChanged;
            if (_decorPlacement != null) _decorPlacement.PlacementChanged += OnDecorPlacementChanged;
            if (_scrollRect != null)
            {
                _lastContentPosition = GetContentPosition();
                _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            }

            _isBound = true;
            _tabBar?.SelectTab(_activeTab);
            RebuildRows();
        }

        public void Refresh()
        {
            if (!_isBound) return;
            RebuildRows();
        }

        public void Teardown()
        {
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            if (_decorPlacement != null) _decorPlacement.PlacementChanged -= OnDecorPlacementChanged;
            if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            CancelRender();
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _cts.Dispose();
            _onRowInfo = null;
            _onRowsRebuilt = null;
            _rows.Clear();
            _isBound = false;
        }

        private void RebuildRows()
        {
            _onRowsRebuilt?.Invoke();
            ClearRows();
            if (_rowPool == null) return;

            _renderCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _rows.Clear();

            for (var i = 0; i < _rowSources.Count; i++)
            {
                var source = _rowSources[i];
                if (source == null) continue;

                foreach (var model in source.BuildRows())
                {
                    var row = _rowPool.GetNext();
                    row.Bind(model, _sprites, _onRowInfo, _renderCts.Token);
                    _rows.Add(new RowEntry(row, source.CategoryId));
                }
            }

            _rowPool.DisableNonActive();
            ApplyTabFilter();
        }

        private void ApplyTabFilter()
        {
            var categoryId = InventoryTabCategories.Resolve(_activeTab);
            for (var i = 0; i < _rows.Count; i++)
            {
                var entry = _rows[i];
                if (entry.View == null) continue;

                var visible = categoryId == null || string.Equals(entry.CategoryId, categoryId, StringComparison.Ordinal);
                if (entry.View.gameObject.activeSelf != visible)
                    entry.View.gameObject.SetActive(visible);
            }
        }

        private void ClearRows()
        {
            CancelRender();
            _rowPool?.DisableAll();
            _rows.Clear();
        }

        private void CancelRender()
        {
            if (_renderCts == null) return;
            _renderCts.Cancel();
            _renderCts.Dispose();
            _renderCts = null;
        }

        private void OnInventoryChanged(InventoryChangeEvent _)
        {
            if (_isBound) RebuildRows();
        }

        private void OnDecorPlacementChanged()
        {
            if (_isBound) RebuildRows();
        }

        private void OnScrollChanged(Vector2 _)
        {
            if (_scrollRect == null)
                return;

            // Content position, not normalizedPosition: the latter degenerates into a constant 0/1 step
            // when the content fits the viewport (a tab with one or two rows) and saturates at the list
            // edges, so real drags produced no delta at all and the info widget stayed open.
            var position = GetContentPosition();
            if ((position - _lastContentPosition).sqrMagnitude
                <= ScrollMoveThresholdPixels * ScrollMoveThresholdPixels)
                return;

            _lastContentPosition = position;
            _onRowsRebuilt?.Invoke();
        }

        private Vector2 GetContentPosition()
        {
            var content = _scrollRect != null ? _scrollRect.content : null;
            return content != null ? content.anchoredPosition : Vector2.zero;
        }

        private void OnTabSelected(TabType tab)
        {
            if (_activeTab == tab)
                return;

            _activeTab = tab;
            _onRowsRebuilt?.Invoke();
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 1f;

            ApplyTabFilter();
        }

        protected override void OnDestroy()
        {
            if (_tabBar != null)
                _tabBar.Selected -= OnTabSelected;

            if (_scrollRect != null)
                _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);

            base.OnDestroy();
        }

        private readonly struct RowEntry
        {
            public RowEntry(InventoryItemRowView view, string categoryId)
            {
                View = view;
                CategoryId = categoryId;
            }

            public InventoryItemRowView View { get; }
            public string CategoryId { get; }
        }
    }
}
