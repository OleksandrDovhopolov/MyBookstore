using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Rewards.API;
using Game.Shop.API;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;

namespace Game.Newspaper.UI
{
    /// <summary>
    /// Daily newspaper window. Phase 0 scope: two decor offers (free + paid) + three book crates
    /// routed through <see cref="IShopService"/>. Future content (book bundles for sale, weather
    /// forecast, weekly calendar) lives in the same prefab/view and lands in later phases.
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

            if (View.CommonBoxBuyButton != null)
                View.CommonBoxBuyButton.onClick.AddListener(OnBuyCommonBoxClicked);
            if (View.RareBoxBuyButton != null)
                View.RareBoxBuyButton.onClick.AddListener(OnBuyRareBoxClicked);
            if (View.DystopicBoxBuyButton != null)
                View.DystopicBoxBuyButton.onClick.AddListener(OnBuyDystopicBoxClicked);

            View.CloseButton.onClick.AddListener(OnCloseClicked);
        }

        protected override void OnShowStart() => RefreshOffers();

        protected override void UpdateWindow() => RefreshOffers();

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (View == null) return;
            View.FreeDecorClaimButton.onClick.RemoveListener(OnClaimFreeDecorClicked);
            View.PaidDecorBuyButton.onClick.RemoveListener(OnBuyPaidDecorClicked);

            if (View.CommonBoxBuyButton != null)
                View.CommonBoxBuyButton.onClick.RemoveListener(OnBuyCommonBoxClicked);
            if (View.RareBoxBuyButton != null)
                View.RareBoxBuyButton.onClick.RemoveListener(OnBuyRareBoxClicked);
            if (View.DystopicBoxBuyButton != null)
                View.DystopicBoxBuyButton.onClick.RemoveListener(OnBuyDystopicBoxClicked);

            View.CloseButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void RefreshOffers()
        {
            if (_shop == null) return;
            RefreshDecorOffers();
            RefreshBookCrates();
        }

        private void RefreshDecorOffers()
        {
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

        private void RefreshBookCrates()
        {
            UpdateCrateLabel(View.CommonBoxLabel, NewspaperShopLotIds.BookBoxCommon15, "Common · 15 books");
            UpdateCrateLabel(View.RareBoxLabel, NewspaperShopLotIds.BookBoxRare8, "Rare · 8 books");
            UpdateCrateLabel(View.DystopicBoxLabel, NewspaperShopLotIds.BookBoxGenreDystopic1, "Dystopic Fiction");
        }

        private void UpdateCrateLabel(TMP_Text label, string lotId, string displayName)
        {
            if (label == null) return;
            if (_shop.TryGetLot(lotId, out var lot))
                label.text = $"{displayName} — <b>{lot.Price.Amount} gold</b>";
            else
                label.text = $"{displayName} (n/a)";
        }

        // ----- decor handlers -----

        private void OnClaimFreeDecorClicked() => ClaimFreeDecorAsync().Forget();

        private async UniTaskVoid ClaimFreeDecorAsync()
        {
            if (_shop == null) return;
            await _shop.BuyAsync(NewspaperShopLotIds.DecorFreeVintageGlobe, _cts.Token);
            RefreshOffers();
        }

        private void OnBuyPaidDecorClicked() => BuyPaidDecorAsync().Forget();

        private async UniTaskVoid BuyPaidDecorAsync()
        {
            if (_shop == null) return;
            var result = await _shop.BuyAsync(NewspaperShopLotIds.DecorPaidCoffeePot, _cts.Token);
            if (result.Status != ShopPurchaseStatus.Success)
                Debug.Log($"[NewspaperWindow] Paid decor purchase failed: {result.Status}.");
            RefreshOffers();
        }

        // ----- book-box handlers -----

        private void OnBuyCommonBoxClicked() => BuyBoxAsync(NewspaperShopLotIds.BookBoxCommon15).Forget();
        private void OnBuyRareBoxClicked() => BuyBoxAsync(NewspaperShopLotIds.BookBoxRare8).Forget();
        private void OnBuyDystopicBoxClicked() => BuyBoxAsync(NewspaperShopLotIds.BookBoxGenreDystopic1).Forget();

        private async UniTaskVoid BuyBoxAsync(string lotId)
        {
            if (_shop == null) return;
            var result = await _shop.BuyAsync(lotId, _cts.Token);
            UpdateLastBookRewardLabel(result);
            RefreshOffers();
        }

        private void UpdateLastBookRewardLabel(ShopPurchaseResult result)
        {
            if (View.LastBookRewardLabel == null) return;
            switch (result.Status)
            {
                case ShopPurchaseStatus.Success:
                    var items = result.Granted?.Items;
                    if (items == null || items.Count == 0)
                    {
                        View.LastBookRewardLabel.text = "Получено: 0 книг (пустой пул).";
                    }
                    else
                    {
                        var ids = string.Join(", ", items.Select(i => i.Id));
                        View.LastBookRewardLabel.text = $"Получено: {items.Count} книг — {ids}";
                    }
                    break;
                case ShopPurchaseStatus.NotEnoughCurrency:
                    View.LastBookRewardLabel.text = "Недостаточно gold.";
                    break;
                case ShopPurchaseStatus.LimitReached:
                    View.LastBookRewardLabel.text = "Закончилось.";
                    break;
                default:
                    View.LastBookRewardLabel.text = $"Ошибка: {result.Status}";
                    break;
            }
        }

        private void OnCloseClicked() => CloseAsync().Forget();
    }
}
