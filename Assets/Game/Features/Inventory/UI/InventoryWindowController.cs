using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Decor;
using Game.Decor.UI;
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
        private IReadOnlyList<IInventoryRowSource> _rowSources;
        private IDecorPlacementService _decorPlacement;

        [Inject]
        public void Construct(
            IInventoryService inventory,
            IUiSpriteProvider sprites,
            IReadOnlyList<IInventoryRowSource> rowSources,
            IDecorPlacementService decorPlacement)
        {
            _inventory = inventory;
            _sprites = sprites;
            _rowSources = rowSources;
            _decorPlacement = decorPlacement;
        }

        protected override void OnInit()
        {
            View.Bind(_inventory, _sprites, _rowSources, _decorPlacement, OnDecorInfoClicked);
        }

        protected override void OnShowStart() => View.Refresh();

        protected override void OnDispose()
        {
            if (View == null) return;
            View.Teardown();
        }

        private void OnDecorInfoClicked(string decorId)
        {
            if (string.IsNullOrEmpty(decorId)) return;
            UIManager.ShowAsync<DecorInfoPopup>(
                new DecorInfoPopupArgs(decorId),
                View != null ? View.destroyCancellationToken : default).Forget();
        }
    }
}
