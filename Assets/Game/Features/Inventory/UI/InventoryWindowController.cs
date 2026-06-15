using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Game.UI;
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

        protected override void OnInit()
        {
            View.Bind(_inventory, _categories, _useRouter, _handlers);
            View.CloseButton.onClick.AddListener(CloseWindow);
        }

        protected override void OnShowStart() => View.Refresh();

        protected override void OnDispose()
        {
            if (View == null) return;
            View.CloseButton.onClick.RemoveListener(CloseWindow);
            View.Teardown();
        }

        private void CloseWindow() => UIManager.HideAsync<InventoryWindowController>().Forget();
    }
}
