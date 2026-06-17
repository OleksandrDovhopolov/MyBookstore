using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory.UI
{
    public class InventoryWindowView : WindowView
    {
        [Header("Tabs")]
        [SerializeField] private Transform _tabContainer;
        [SerializeField] private Button _tabButtonPrefab;

        [Header("List")]
        [SerializeField] private Transform _rowContainer;
        [SerializeField] private InventoryItemRowView _rowPrefab;

        [Header("Header")]
        [SerializeField] private TMP_Text _activeCategoryLabel;

        [SerializeField] private Button _closeButton;

        private IInventoryService _inventory;
        private IItemCategoryRegistry _categories;
        private IInventoryUseRouter _useRouter;
        private IReadOnlyList<IInventoryItemUseHandler> _handlers;

        private readonly List<Button> _tabButtons = new();
        private readonly List<InventoryItemRowView> _rows = new();
        private readonly HashSet<string> _categoriesWithHandlers = new();
        private readonly Dictionary<string, IInventoryItemInfoProvider> _infoByCategoryId = new();
        private string _activeCategoryId;
        private readonly CancellationTokenSource _cts = new();
        private bool _isBound;

        public Button CloseButton => _closeButton;

        /// <summary>
        /// Called once by the controller in OnInit. Stores deps, builds tabs, subscribes to inventory
        /// changes and selects the first category. Subsequent re-shows go through <see cref="Refresh"/>.
        /// </summary>
        public void Bind(
            IInventoryService inventory,
            IItemCategoryRegistry categories,
            IInventoryUseRouter useRouter,
            IReadOnlyList<IInventoryItemUseHandler> handlers,
            IReadOnlyList<IInventoryItemInfoProvider> infoProviders)
        {
            if (_isBound) return;

            _inventory = inventory;
            _categories = categories;
            _useRouter = useRouter;
            _handlers = handlers;

            if (_inventory == null || _categories == null)
            {
                Debug.LogWarning("[InventoryWindowView] dependencies missing — not registered in DI?");
                return;
            }

            for (var i = 0; _handlers != null && i < _handlers.Count; i++)
                if (_handlers[i] != null) _categoriesWithHandlers.Add(_handlers[i].SupportedCategoryId);

            if (infoProviders != null)
            {
                for (var i = 0; i < infoProviders.Count; i++)
                {
                    var p = infoProviders[i];
                    if (p == null || string.IsNullOrEmpty(p.SupportedCategoryId)) continue;
                    _infoByCategoryId[p.SupportedCategoryId] = p;
                }
            }

            BuildTabs();
            _inventory.Changed += OnInventoryChanged;

            var first = _categories.GetAll();
            if (first.Count > 0) SelectCategory(first[0].Id);

            _isBound = true;
        }

        public void Refresh()
        {
            if (!_isBound || string.IsNullOrEmpty(_activeCategoryId)) return;
            Render();
        }

        public void Teardown()
        {
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _cts.Dispose();
            _isBound = false;
        }

        private void BuildTabs()
        {
            if (_tabContainer == null || _tabButtonPrefab == null) return;

            var all = _categories.GetAll();
            for (var i = 0; i < all.Count; i++)
            {
                var category = all[i];
                var button = Instantiate(_tabButtonPrefab, _tabContainer);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = category.DisplayName;
                var capturedId = category.Id;
                button.onClick.AddListener(() => SelectCategory(capturedId));
                _tabButtons.Add(button);
            }
        }

        private void SelectCategory(string categoryId)
        {
            _activeCategoryId = categoryId;

            if (_activeCategoryLabel != null && _categories.TryGet(categoryId, out var c))
                _activeCategoryLabel.text = c.DisplayName;

            Render();
        }

        private void Render()
        {
            ClearRows();
            if (_rowPrefab == null || _rowContainer == null || string.IsNullOrEmpty(_activeCategoryId)) return;

            var items = _inventory.GetByCategory(_activeCategoryId);
            var hasHandler = _categoriesWithHandlers.Contains(_activeCategoryId);
            _infoByCategoryId.TryGetValue(_activeCategoryId, out var infoProvider);
            for (var i = 0; i < items.Count; i++)
            {
                var info = infoProvider?.GetInfoFor(items[i].ItemId);
                var row = Instantiate(_rowPrefab, _rowContainer);
                row.Bind(items[i], hasHandler, info, OnUseClicked, i);
                _rows.Add(row);
            }
        }

        private void ClearRows()
        {
            for (var i = 0; i < _rows.Count; i++)
                if (_rows[i] != null) Destroy(_rows[i].gameObject);
            _rows.Clear();
        }

        private void OnInventoryChanged(InventoryChangeEvent _)
        {
            if (_isBound && !string.IsNullOrEmpty(_activeCategoryId)) Render();
        }

        private void OnUseClicked(string itemId) => UseAsync(itemId, _cts.Token).Forget();

        private async UniTaskVoid UseAsync(string itemId, CancellationToken ct)
        {
            var result = await _useRouter.UseAsync(itemId, ct);
            Debug.Log($"[InventoryWindow] use {itemId}: success={result.Success}, consume={result.ConsumeAfterUse}, msg={result.Message}");
        }
    }
}
