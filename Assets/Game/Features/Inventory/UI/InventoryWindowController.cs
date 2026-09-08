using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Decor;
using Game.Decor.UI;
using Game.Inventory.API;
using Game.Localization;
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
        private const string TodoDescriptionKey = "ui.inventory.item.description.placeholder";

        private IInventoryService _inventory;
        private IUiSpriteProvider _sprites;
        private IReadOnlyList<IInventoryRowSource> _rowSources;
        private IDecorPlacementService _decorPlacement;

        // The widget is hidden by instance, not by type: UIManager.HideAsync<T> and IsWindowShown
        // both filter on IWindowController.IsShown, which is only set after the show animation
        // finishes. Scroll events arriving before that would otherwise be dropped silently.
        private IWindowController _itemInfoWidget;
        private int _pendingWidgetShows;
        private bool _hideRequestedWhileShowing;

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

        protected override void OnDispose()
        {
            // The widget itself is force-closed by the UIManager parent cascade (ContentWidgetArgs
            // carries this controller as ParentWindow); here we only drop our subscription.
            UntrackItemInfoWidget();

            if (View == null) return;
            View.Teardown();
        }

        private void OnRowInfoClicked(string itemId, InventoryRowStyle style, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            if (style == InventoryRowStyle.Decor)
            {
                ShowDecorInfo(itemId);
            }
            else
            {
                ShowItemInfoWidgetAsync(itemId, anchor).Forget();
            }
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

            // A fresh show supersedes a hide that was requested while the previous one was still running.
            _hideRequestedWhileShowing = false;
            _pendingWidgetShows++;

            try
            {
                var data = new InventoryItemWidgetData(itemId, LocalizationLocator.GetOrKey(TodoDescriptionKey));
                var args = new ContentWidgetArgs(
                    data,
                    anchor,
                    this,
                    placementMode: ContentWidgetPlacementMode.HorizontalOnly);
                TrackItemInfoWidget(
                    await UIManager.ShowAsync<ContentWidgetController>(args, View.destroyCancellationToken));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventoryWindowController] Failed to show item widget for '{itemId}': {e}");
            }
            finally
            {
                _pendingWidgetShows--;

                if (_pendingWidgetShows == 0 && _hideRequestedWhileShowing)
                {
                    _hideRequestedWhileShowing = false;
                    HideItemInfoWidget();
                }
            }
        }

        private void TrackItemInfoWidget(IWindowController widget)
        {
            // null means the show was rejected by the window filter — keep tracking whatever is up.
            if (widget == null || ReferenceEquals(_itemInfoWidget, widget)) return;

            UntrackItemInfoWidget();
            _itemInfoWidget = widget;

            // The widget also closes on its own (auto-close timer, close button, another page opening),
            // so drop the reference on Closed instead of hiding an already hidden controller later.
            _itemInfoWidget.Closed += OnItemInfoWidgetClosed;
        }

        private void UntrackItemInfoWidget()
        {
            if (_itemInfoWidget == null) return;

            _itemInfoWidget.Closed -= OnItemInfoWidgetClosed;
            _itemInfoWidget = null;
        }

        private void OnItemInfoWidgetClosed(IWindowController _) => UntrackItemInfoWidget();

        private void HideItemInfoWidget()
        {
            if (UIManager == null) return;

            // ShowAsync is still holding the UIManager gate: the controller is not IsShown yet, so a
            // hide issued now would be dropped. Replay it once the show completes.
            if (_pendingWidgetShows > 0)
            {
                _hideRequestedWhileShowing = true;
                return;
            }

            var widget = _itemInfoWidget;
            if (widget == null) return;

            UntrackItemInfoWidget();
            UIManager.HideAsync(widget, forceClose: true, ct: CancellationToken.None).Forget();
        }
    }
}
