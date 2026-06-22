using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shop.API;
using Game.UI;
using Game.UI.Common;
using UIShared;
using UnityEngine;
using VContainer;

namespace Game.Newspaper.UI
{
    [Window("NewspaperWindow", WindowType.Page)]
    public sealed class NewspaperWindow : WindowController<NewspaperWindowView>
    {
        private IShopService _shop;
        private IShopConfirmationPolicy _confirmPolicy;
        private INewspaperOfferSource _offerSource;
        private CancellationTokenSource _cts;

        [Inject]
        public void InjectServices(
            IShopService shop,
            IShopConfirmationPolicy confirmPolicy,
            INewspaperOfferSource offerSource)
        {
            _shop = shop;
            _confirmPolicy = confirmPolicy;
            _offerSource = offerSource;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();
        }

        protected override void OnShowStart() => RefreshOffers();

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            View?.BookCardsPool?.DisableAll();
            View?.DecorCardsPool?.DisableAll();
        }

        private void RefreshOffers()
        {
            if (_offerSource == null || View == null) return;

            View.BookCardsPool.DisableAll();
            View.DecorCardsPool.DisableAll();
            SpawnOffers(_offerSource.GetBookOffers(), View.BookCardsPool);
            SpawnOffers(_offerSource.GetDecorOffers(), View.DecorCardsPool);
            View.BookCardsPool.DisableNonActive();
            View.DecorCardsPool.DisableNonActive();
        }

        private void SpawnOffers(IReadOnlyList<NewspaperOffer> offers, UIListPool<NewspaperOfferCardView> pool)
        {
            if (offers == null || offers.Count == 0 || pool == null) return;

            for (var i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                if (offer == null) continue;

                var card = pool.GetNext();
                var capturedLotId = offer.LotId;
                card.Bind(offer, () => TryBuyAsync(capturedLotId).Forget());
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

            if (_confirmPolicy != null && _confirmPolicy.RequiresConfirmation(lot))
            {
                var confirmed = await ShowConfirmAsync(lot);
                if (!confirmed) return;
            }

            var result = await _shop.BuyAsync(lotId, _cts.Token);

            if (result.Status == ShopPurchaseStatus.Success && result.Granted != null
                && result.Granted.Items.Count > 0)
            {
                await UIManager.ShowAsync<RewardsWindow>(
                    new RewardsWindowArgs(result.Granted, $"Received from {lot.RewardId}"),
                    _cts.Token);
            }
            else if (result.Status != ShopPurchaseStatus.Success)
            {
                Debug.Log($"[NewspaperWindow] Purchase '{lotId}' failed: {result.Status}.");
            }

            RefreshOffers();
        }

        private async UniTask<bool> ShowConfirmAsync(ShopLot lot)
        {
            var args = new ConfirmDialogArgs(
                title: $"Buy {lot.DisplayName ?? lot.RewardId}?",
                body: $"Spend <b>{lot.Price.Amount} {lot.Price.Currency}</b> on this offer?",
                confirmLabel: "Buy",
                cancelLabel: "Cancel");

            var dialog = await UIManager.ShowAsync<ConfirmDialog>(args, _cts.Token);
            if (dialog == null) return false;

            var result = await dialog.WaitForResultAsync<ConfirmDialogResult>(_cts.Token);
            return result == ConfirmDialogResult.Confirmed;
        }
    }
}
