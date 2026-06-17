using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Rewards.API;
using Game.Shop.API;
using Game.UI;
using Game.UI.Common;
using UnityEngine;
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
        private IShopConfirmationPolicy _confirmPolicy;
        private CancellationTokenSource _cts;

        [Inject]
        public void InjectServices(IShopService shop, IShopConfirmationPolicy confirmPolicy)
        {
            _shop = shop;
            _confirmPolicy = confirmPolicy;
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

        // ----- click handlers (all flow through TryBuyAsync) -----

        private void OnClaimFreeDecorClicked() =>
            TryBuyAsync(NewspaperShopLotIds.DecorFreeVintageGlobe, showRewardsPopup: true, updateBookLabel: false).Forget();

        private void OnBuyPaidDecorClicked() =>
            TryBuyAsync(NewspaperShopLotIds.DecorPaidCoffeePot, showRewardsPopup: true, updateBookLabel: false).Forget();

        private void OnBuyCommonBoxClicked() =>
            TryBuyAsync(NewspaperShopLotIds.BookBoxCommon15, showRewardsPopup: true, updateBookLabel: true).Forget();

        private void OnBuyRareBoxClicked() =>
            TryBuyAsync(NewspaperShopLotIds.BookBoxRare8, showRewardsPopup: true, updateBookLabel: true).Forget();

        private void OnBuyDystopicBoxClicked() =>
            TryBuyAsync(NewspaperShopLotIds.BookBoxGenreDystopic1, showRewardsPopup: true, updateBookLabel: true).Forget();

        // ----- common purchase pipeline -----

        private async UniTaskVoid TryBuyAsync(string lotId, bool showRewardsPopup, bool updateBookLabel)
        {
            if (_shop == null) return;

            if (!_shop.TryGetLot(lotId, out var lot))
            {
                Debug.LogWarning($"[NewspaperWindow] Lot '{lotId}' not found in catalog.");
                return;
            }

            // PR9: confirmation policy (default: price > 50g). Free + cheap lots skip the dialog.
            if (_confirmPolicy != null && _confirmPolicy.RequiresConfirmation(lot))
            {
                var confirmed = await ShowConfirmAsync(lot);
                if (!confirmed) return;
            }

            var result = await _shop.BuyAsync(lotId, _cts.Token);

            // PR10: show RewardsWindow on success; keep inline label for failures + decor errors.
            if (result.Status == ShopPurchaseStatus.Success && showRewardsPopup && result.Granted != null
                && result.Granted.Items.Count > 0)
            {
                await UIManager.ShowAsync<RewardsWindow>(
                    new RewardsWindowArgs(result.Granted, $"Received from {lot.RewardId}"),
                    _cts.Token);
            }

            if (updateBookLabel)
                UpdateLastBookRewardLabel(result);
            else if (result.Status != ShopPurchaseStatus.Success)
                Debug.Log($"[NewspaperWindow] Purchase '{lotId}' failed: {result.Status}.");

            RefreshOffers();
        }

        private async UniTask<bool> ShowConfirmAsync(ShopLot lot)
        {
            var args = new ConfirmDialogArgs(
                title: $"Buy {lot.RewardId}?",
                body: $"Spend <b>{lot.Price.Amount} {lot.Price.Currency}</b> on this offer?",
                confirmLabel: "Buy",
                cancelLabel: "Cancel");

            var dialog = await UIManager.ShowAsync<ConfirmDialog>(args, _cts.Token);
            if (dialog == null) return false;

            var result = await dialog.WaitForResultAsync<ConfirmDialogResult>(_cts.Token);
            return result == ConfirmDialogResult.Confirmed;
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
