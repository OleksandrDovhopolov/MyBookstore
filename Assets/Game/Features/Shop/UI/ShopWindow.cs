using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Decor.UI;
using Game.Localization;
using Game.Rewards.UI;
using Game.Shop;
using Game.Shop.API;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using UIShared;
using UnityEngine;
using VContainer;

namespace Game.Shop.UI
{
    [Window("NewspaperWindow", WindowType.Page, keepInCache: true)]
    public sealed class ShopWindow : WindowController<ShopWindowView>
    {
        private const string TodoDescriptionKey = "ui.shop.item.description.placeholder";

        private IShopService _shop;
        private IShopConfirmationPolicy _confirmPolicy;
        private IShopOfferSource _offerSource;
        private IUiSpriteProvider _uiSprites;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _iconsCts;
        private TabType _activeTab = TabType.All;
        private readonly Dictionary<string, ShopItemView> _cardsByLotId = new(StringComparer.Ordinal);
        private IWindowController _itemInfoWidget;
        private int _pendingWidgetShows;
        private bool _hideRequestedWhileShowing;

        [Inject]
        public void InjectServices(
            IShopService shop,
            IShopConfirmationPolicy confirmPolicy,
            IShopOfferSource offerSource,
            IUiSpriteProvider uiSprites)
        {
            _shop = shop;
            _confirmPolicy = confirmPolicy;
            _offerSource = offerSource;
            _uiSprites = uiSprites;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();
        }

        protected override void OnShowStart()
        {
            if (View != null)
            {
                View.TabSelected -= OnTabSelected;
                View.TabSelected += OnTabSelected;
                View.Scrolled -= HideItemInfoWidget;
                View.Scrolled += HideItemInfoWidget;
                View.SelectTab(_activeTab);
            }

            RefreshOffers();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (View != null)
            {
                View.TabSelected -= OnTabSelected;
                View.Scrolled -= HideItemInfoWidget;
            }

            HideItemInfoWidget();
            CancelIconLoad();
            base.OnHideStart(isClosed);
        }

        protected override void OnDispose()
        {
            if (View != null)
            {
                View.TabSelected -= OnTabSelected;
                View.Scrolled -= HideItemInfoWidget;
            }

            UntrackItemInfoWidget();
            CancelIconLoad();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            View?.CardsPool?.DisableAll();
            _cardsByLotId.Clear();
        }

        private void RefreshOffers()
        {
            HideItemInfoWidget();

            if (_offerSource == null || View == null) return;

            var pool = View.CardsPool;
            if (pool == null) return;

            pool.DisableAll();
            _cardsByLotId.Clear();
            SpawnOffers(ShopTabOffers.Build(_offerSource, _activeTab), pool);
            pool.DisableNonActive();
            LoadOfferIconsForCurrentPool();
        }

        private void OnTabSelected(TabType tab)
        {
            if (_activeTab == tab) return;

            _activeTab = tab;
            RefreshOffers();
        }

        private void SpawnOffers(
            IReadOnlyList<ShopOffer> offers,
            UIListPool<ShopItemView> pool)
        {
            if (offers == null || offers.Count == 0 || pool == null) return;

            for (var i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                if (offer == null) continue;

                var card = pool.GetNext();
                var capturedLotId = offer.LotId;
                card.Bind(
                    offer,
                    () => TryBuyAsync(capturedLotId).Forget(),
                    onInfoClicked: OnOfferInfoClicked);
                if (!string.IsNullOrEmpty(offer.LotId))
                    _cardsByLotId[offer.LotId] = card;
            }
        }

        private void OnOfferInfoClicked(string lotId, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(lotId) || !TryGetCurrentOffer(lotId, out var offer) || offer == null)
                return;

            HideItemInfoWidget();

            if (offer.IsDecor)
            {
                ShowDecorInfo(offer.IconId);
                return;
            }

            ShowItemInfoWidgetAsync(lotId, anchor).Forget();
        }

        private void ShowDecorInfo(string decorId)
        {
            if (string.IsNullOrEmpty(decorId)) return;

            UIManager.ShowAsync<DecorInfoPopup>(
                new DecorInfoPopupArgs(decorId),
                _cts != null ? _cts.Token : default).Forget();
        }

        private async UniTaskVoid LoadOfferIconsAsync(CancellationToken ct)
        {
            if (View == null || _uiSprites == null) return;

            try
            {
                await LoadIconsForPoolAsync(View.CardsPool, ct);
            }
            catch (OperationCanceledException)
            {
                // Window hidden or tab switched mid-load: the next refresh owns the visible cards.
            }
        }

        private void LoadOfferIconsForCurrentPool()
        {
            CancelIconLoad();

            if (_cts == null || View == null || _uiSprites == null) return;

            _iconsCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            LoadOfferIconsAsync(_iconsCts.Token).Forget();
        }

        private void CancelIconLoad()
        {
            _iconsCts?.Cancel();
            _iconsCts?.Dispose();
            _iconsCts = null;
        }

        private async UniTask LoadIconsForPoolAsync(
            UIListPool<ShopItemView> pool,
            CancellationToken ct)
        {
            if (pool == null) return;

            // Snapshot: ActiveElements() is a lazy iterator over the live pool; awaiting inside a
            // foreach over it would break if the window is closed while icons are loading.
            var cards = pool.ActiveElements().ToList();
            foreach (var card in cards)
            {
                if (card == null) continue;

                var iconId = card.IconId;
                var sprite = await _uiSprites.GetSpriteAsync(iconId, ct);
                if (ct.IsCancellationRequested) return;
                if (card == null || !string.Equals(card.IconId, iconId, StringComparison.Ordinal)) continue;

                card.SetIcon(sprite);
                var bookIconId = card.BookIconId;
                if (string.IsNullOrEmpty(bookIconId)) continue;

                var bookSprite = await _uiSprites.GetSpriteAsync(bookIconId, ct);
                if (ct.IsCancellationRequested) return;
                if (card != null && string.Equals(card.BookIconId, bookIconId, StringComparison.Ordinal))
                    card.SetBookIcon(bookSprite);
            }
        }

        private async UniTaskVoid ShowItemInfoWidgetAsync(string lotId, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(lotId) || anchor == null || UIManager == null || View == null)
                return;

            _hideRequestedWhileShowing = false;
            _pendingWidgetShows++;

            try
            {
                var data = new ShopItemWidgetData(lotId, LocalizationLocator.GetOrKey(TodoDescriptionKey));
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
                Debug.LogError($"[NewspaperWindow] Failed to show item widget for '{lotId}': {e}");
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
            if (widget == null || ReferenceEquals(_itemInfoWidget, widget)) return;

            UntrackItemInfoWidget();
            _itemInfoWidget = widget;
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

        private async UniTaskVoid TryBuyAsync(string lotId)
        {
            if (_shop == null || string.IsNullOrEmpty(lotId)) return;

            if (!_shop.TryGetLot(lotId, out var lot))
            {
                Debug.LogWarning($"[NewspaperWindow] Lot '{lotId}' not found in catalog.");
                return;
            }

            var result = await _shop.BuyAsync(lotId, _cts.Token);

            if (result.Status == ShopPurchaseStatus.Success)
            {
                UpdatePurchasedCard(lotId);

                if (result.Granted != null && result.Granted.Items.Count > 0)
                {
                    await UIManager.ShowAsync<RewardsWindow>(
                        new RewardsWindowArgs(result.Granted, $"Received from {lot.RewardId}"),
                        _cts.Token);
                }
            }
            else if (result.Status != ShopPurchaseStatus.Success)
            {
                if (result.Status == ShopPurchaseStatus.NotEnoughCurrency)
                    ShowInfoWidget(LocalizationLocator.GetOrKey(ShopUiTexts.NotEnoughGold));

                Debug.Log($"[NewspaperWindow] Purchase '{lotId}' failed: {result.Status}.");
            }
        }

        private void ShowInfoWidget(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            UIManager.ShowAsync<InfoWidgetController>(new InfoWidgetArg { Text = text }).Forget();
        }

        private void UpdatePurchasedCard(string lotId)
        {
            if (string.IsNullOrEmpty(lotId) || !_cardsByLotId.TryGetValue(lotId, out var card) || card == null)
            {
                return;
            }

            if (TryGetCurrentOffer(lotId, out var offer))
                card.UpdateOfferState(offer);
        }

        private bool TryGetCurrentOffer(string lotId, out ShopOffer offer)
        {
            offer = null;
            if (_offerSource == null || string.IsNullOrEmpty(lotId)) return false;

            return TryFindOffer(_offerSource.GetBookOffers(), lotId, out offer)
                   || TryFindOffer(_offerSource.GetConsumableOffers(), lotId, out offer)
                   || TryFindOffer(_offerSource.GetDecorOffers(), lotId, out offer);
        }

        private static bool TryFindOffer(
            IReadOnlyList<ShopOffer> offers,
            string lotId,
            out ShopOffer offer)
        {
            offer = null;
            if (offers == null) return false;

            for (var i = 0; i < offers.Count; i++)
            {
                var candidate = offers[i];
                if (candidate == null || !string.Equals(candidate.LotId, lotId, StringComparison.Ordinal)) continue;

                offer = candidate;
                return true;
            }

            return false;
        }
    }
}
