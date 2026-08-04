using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Decor;
using Game.Decor.UI;
using Game.Inventory.API;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using UnityEngine;
using VContainer;

namespace Game.Inventory.UI
{
    [Window("InventoryWindow", WindowType.Page)]
    public sealed class InventoryWindowController : WindowController<InventoryWindowView>
    {
        private const string TodoDescription = "TODO: item description";

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
            View.Bind(_inventory, _sprites, _rowSources, _decorPlacement, OnRowInfoClicked, HideItemInfoWidget);
        }

        protected override void OnShowStart() => View.Refresh();

        protected override void OnDispose()
        {
            if (View == null) return;
            View.Teardown();
        }

        private void OnRowInfoClicked(string itemId, InventoryRowStyle style, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            HideItemInfoWidget();
            if (style == InventoryRowStyle.Decor)
            {
                ShowDecorInfo(itemId);
                return;
            }

            ShowItemInfoWidgetAsync(itemId, anchor).Forget();
        }

        private void ShowDecorInfo(string decorId)
        {
            if (string.IsNullOrEmpty(decorId)) return;
            UIManager.ShowAsync<DecorInfoPopup>(
                new DecorInfoPopupArgs(decorId),
                View != null ? View.destroyCancellationToken : default).Forget();
        }

        private async UniTaskVoid ShowItemInfoWidgetAsync(string itemId, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(itemId) || anchor == null || UIManager == null || View == null)
                return;

            try
            {
                var data = new InventoryItemWidgetData(itemId, TodoDescription);
                var args = new ContentWidgetArgs(
                    data,
                    anchor,
                    this,
                    placementMode: ContentWidgetPlacementMode.HorizontalOnly);
                await UIManager.ShowAsync<ContentWidgetController>(args, View.destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventoryWindowController] Failed to show item widget for '{itemId}': {e}");
            }
        }

        private void HideItemInfoWidget()
        {
            if (UIManager == null || !UIManager.IsWindowShown<ContentWidgetController>())
                return;

            UIManager.HideAsync<ContentWidgetController>(
                forceClose: true,
                ct: CancellationToken.None).Forget();
        }
    }
}
