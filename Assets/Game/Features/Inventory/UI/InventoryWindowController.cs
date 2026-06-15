using System.Collections.Generic;
using Game.Inventory.API;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Inventory.UI
{
    [Window("InventoryWindow", WindowType.Page)]
    public sealed class InventoryWindowController : WindowController<InventoryWindowView>
    {
        private IInventoryService _inventory;
        private IItemCategoryRegistry _categories;
        private IInventoryUseRouter _useRouter;
        private IReadOnlyList<IInventoryItemUseHandler> _handlers;
        
        [Inject]
        public void Construct(
            IInventoryService inventory,
            IItemCategoryRegistry categories,
            IInventoryUseRouter useRouter,
            IReadOnlyList<IInventoryItemUseHandler> handlers)
        {
            _inventory = inventory;
            _categories = categories;
            _useRouter = useRouter;
            _handlers = handlers;
        }
        
        protected override void OnShowStart()
        {
            Debug.Log("[Smoke] InventoryWindowController ShowStart");
            
            View.Init(_inventory, _categories, _useRouter, _handlers);
        }

        protected override void OnShowComplete()
        {
            Debug.Log("[Smoke] InventoryWindowController OnShowComplete");
            base.OnShowComplete();
            View.CloseButton.onClick.AddListener(CloseWindow);
        }

        private void CloseWindow()
        {
            UIManager.HideAsync<InventoryWindowController>();
        }
        
        protected override void OnHideStart(bool isClosed)
        {
            Debug.Log("[Smoke] InventoryWindowController OnHideStart");
            
            base.OnHideStart(isClosed);
            
            View.CloseButton.onClick.RemoveAllListeners();
        }
    }
}
