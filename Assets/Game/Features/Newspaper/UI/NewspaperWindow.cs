using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shop.API;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Newspaper.UI
{
    /// <summary>
    /// Daily newspaper window. Phase 0 scope: two decor offers (free + paid) routed through
    /// <see cref="IShopService"/>. Future content (book bundles for sale, weather forecast,
    /// weekly calendar) lives in the same prefab/view and lands in later phases.
    /// </summary>
    [Window("NewspaperWindow", WindowType.Page)]
    public sealed class NewspaperWindow : WindowController<NewspaperWindowView>
    {
        private IShopService _shop;
        private CancellationTokenSource _cts;

        [Inject]
        public void InjectServices(IShopService shop)
        {
            _shop = shop;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();
            View.FreeDecorClaimButton.onClick.AddListener(OnClaimFreeDecorClicked);
            View.PaidDecorBuyButton.onClick.AddListener(OnBuyPaidDecorClicked);
            View.CloseButton.onClick.AddListener(OnCloseClicked);
        }

        protected override void OnShowStart() => RefreshDecorOffers();

        protected override void UpdateWindow() => RefreshDecorOffers();

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (View == null) return;
            View.FreeDecorClaimButton.onClick.RemoveListener(OnClaimFreeDecorClicked);
            View.PaidDecorBuyButton.onClick.RemoveListener(OnBuyPaidDecorClicked);
            View.CloseButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void RefreshDecorOffers()
        {
            if (_shop == null) return;

            if (View.FreeDecorPanel != null)
            {
                var available = _shop.IsAvailable(NewspaperShopLotIds.DecorFreeVintageGlobe);
                View.FreeDecorPanel.SetActive(available);
                if (available && View.FreeDecorLabel != null
                    && _shop.TryGetLot(NewspaperShopLotIds.DecorFreeVintageGlobe, out var freeLot))
                    View.FreeDecorLabel.text = $"Newspaper: free <b>{freeLot.RewardId}</b>!";
            }

            if (View.PaidDecorPanel != null)
            {
                var available = _shop.IsAvailable(NewspaperShopLotIds.DecorPaidCoffeePot);
                View.PaidDecorPanel.SetActive(available);
                if (available && View.PaidDecorLabel != null
                    && _shop.TryGetLot(NewspaperShopLotIds.DecorPaidCoffeePot, out var paidLot))
                    View.PaidDecorLabel.text = $"Buy <b>{paidLot.RewardId}</b> — {paidLot.Price.Amount} gold";
            }
        }

        private void OnClaimFreeDecorClicked() => ClaimFreeDecorAsync().Forget();

        private async UniTaskVoid ClaimFreeDecorAsync()
        {
            if (_shop == null) return;
            await _shop.BuyAsync(NewspaperShopLotIds.DecorFreeVintageGlobe, _cts.Token);
            RefreshDecorOffers();
        }

        private void OnBuyPaidDecorClicked() => BuyPaidDecorAsync().Forget();

        private async UniTaskVoid BuyPaidDecorAsync()
        {
            if (_shop == null) return;
            var result = await _shop.BuyAsync(NewspaperShopLotIds.DecorPaidCoffeePot, _cts.Token);
            if (result.Status != ShopPurchaseStatus.Success)
                Debug.Log($"[NewspaperWindow] Paid decor purchase failed: {result.Status}.");
            RefreshDecorOffers();
        }

        private void OnCloseClicked() => CloseAsync().Forget();
    }
}
