using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Decor.UI;
using Game.Rewards.UI;
using Game.Shop;
using Game.Shop.API;
using Game.UI;
using SpriteService;
using UIShared;
using UnityEngine;
using VContainer;

namespace Game.Shop.UI
{
    [Window("NewspaperWindow", WindowType.Page, keepInCache: true)]
    public sealed class ShopWindow : WindowController<ShopWindowView>
    {
        private IShopService _shop;
        private IShopConfirmationPolicy _confirmPolicy;
        private IShopOfferSource _offerSource;
        private IUiSpriteProvider _uiSprites;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _iconsCts;
        private ShopTab _activeTab = ShopTab.All;
        private readonly Dictionary<string, ShopItemView> _cardsByLotId = new(StringComparer.Ordinal);

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
                View.SelectTab(_activeTab);
            }

            RefreshOffers();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (View != null)
                View.TabSelected -= OnTabSelected;

            CancelIconLoad();
            base.OnHideStart(isClosed);
        }

        protected override void OnDispose()
        {
            if (View != null)
                View.TabSelected -= OnTabSelected;

            CancelIconLoad();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            View?.CardsPool?.DisableAll();
            _cardsByLotId.Clear();
        }

        private void RefreshOffers()
        {
            if (_offerSource == null || View == null) return;

            var pool = View.CardsPool;
            if (pool == null) return;

            pool.DisableAll();
            _cardsByLotId.Clear();
            SpawnOffers(ShopTabOffers.Build(_offerSource, _activeTab), pool);
            pool.DisableNonActive();
            LoadOfferIconsForCurrentPool();
        }

        private void OnTabSelected(ShopTab tab)
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
                    onDecorInfoClicked: ShowDecorInfo);
                if (!string.IsNullOrEmpty(offer.LotId))
                    _cardsByLotId[offer.LotId] = card;
            }
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

                var sprite = await _uiSprites.GetSpriteAsync(card.IconId, ct);
                if (ct.IsCancellationRequested) return;
                if (card != null) card.SetIcon(sprite);
            }
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
                    ShowInfoWidget(ShopUiTexts.NotEnoughGold);

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
