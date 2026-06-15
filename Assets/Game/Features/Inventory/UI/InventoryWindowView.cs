using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

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
        private string _activeCategoryId;
        private readonly CancellationTokenSource _cts = new();

        public Button CloseButton => _closeButton;
        
        public void Init(IInventoryService inventory, IItemCategoryRegistry categories, IInventoryUseRouter useRouter, IReadOnlyList<IInventoryItemUseHandler> handlers)
        {
            _inventory = inventory;
            _categories = categories;
            _useRouter = useRouter;
            _handlers = handlers;

            Init();
        }
        
        private void Init()
        {
            if (_inventory == null || _categories == null)
            {
                Debug.LogWarning("[InventoryScreenView] dependencies missing — not registered in DI?");
                return;
            }

            for (var i = 0; _handlers != null && i < _handlers.Count; i++)
                if (_handlers[i] != null) _categoriesWithHandlers.Add(_handlers[i].SupportedCategoryId);

            BuildTabs();
            _inventory.Changed += OnInventoryChanged;

            var first = _categories.GetAll();
            if (first.Count > 0) SelectCategory(first[0].Id);
        }

        public void BuildTabs()
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

        public void Render()
        {
            ClearRows();
            if (_rowPrefab == null || _rowContainer == null || string.IsNullOrEmpty(_activeCategoryId)) return;

            var items = _inventory.GetByCategory(_activeCategoryId);
            var hasHandler = _categoriesWithHandlers.Contains(_activeCategoryId);
            for (var i = 0; i < items.Count; i++)
            {
                var row = Instantiate(_rowPrefab, _rowContainer);
                row.Bind(items[i], hasHandler, OnUseClicked);
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
            if (!string.IsNullOrEmpty(_activeCategoryId)) Render();
        }

        private void OnUseClicked(string itemId)
        {
            UseAsync(itemId, _cts.Token).Forget();
        }

        private async UniTaskVoid UseAsync(string itemId, CancellationToken ct)
        {
            var result = await _useRouter.UseAsync(itemId, ct);
            Debug.Log($"[InventoryScreen] use {itemId}: success={result.Success}, consume={result.ConsumeAfterUse}, msg={result.Message}");
        }
    }
}
