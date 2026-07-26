using Game.Configs;
using Game.Inventory.API;
using Game.UI;
using SpriteService;
using VContainer;

namespace Game.Inventory.UI
{
    [Window("InventoryWindow", WindowType.Page)]
    public sealed class InventoryWindowController : WindowController<InventoryWindowView>
    {
        private IInventoryService _inventory;
        private IUiSpriteProvider _sprites;
        private IConfigsService _configs;

        [Inject]
        public void Construct(
            IInventoryService inventory,
            IUiSpriteProvider sprites,
            IConfigsService configs)
        {
            _inventory = inventory;
            _sprites = sprites;
            _configs = configs;
        }

        protected override void OnInit()
        {
            View.Bind(_inventory, _sprites, _configs);
        }

        protected override void OnShowStart() => View.Refresh();

        protected override void OnDispose()
        {
            if (View == null) return;
            View.Teardown();
        }
    }
}
