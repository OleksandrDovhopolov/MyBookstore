using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Newspaper.UI;
using Game.Rewards.API;
using Game.Shop.API;
using Game.UI;
using Game.UI.Common;
using TMPro;
using UnityEngine;

namespace Game.Shop.UI.Sections
{
    /// <summary>
    /// Generic shop section. Bound to one storefront id at runtime — renders all lots from that
    /// storefront as <see cref="ShopLotCardView"/>s and runs the purchase flow (confirmation,
    /// rewards popup) per click. Reused by <see cref="ClassicShopWindow"/> for Books/Boxes/Decor
    /// tabs; Phase 1.5 refactor may pull NewspaperWindow into the same pattern.
    /// </summary>
    public sealed class ShopLotsSectionView : MonoBehaviour
    {
        [SerializeField] private Transform _cardContainer;
        [SerializeField] private ShopLotCardView _cardTemplate;     // hidden in OnEnable; cloned per lot
        [SerializeField] private TMP_Text _emptyLabel;              // shown when storefront yields no lots

        private readonly List<ShopLotCardView> _pool = new();
        private string _storefrontId;
        private IShopService _shop;
        private IShopConfirmationPolicy _confirmPolicy;
        private IUIManager _uiManager;
        private CancellationToken _ct;
        private bool _bound;

        private void Awake()
        {
            if (_cardTemplate != null) _cardTemplate.gameObject.SetActive(false);
        }

        public void Bind(string storefrontId, IShopService shop, IShopConfirmationPolicy confirmPolicy,
                         IUIManager uiManager, CancellationToken ct)
        {
            _storefrontId = storefrontId;
            _shop = shop;
            _confirmPolicy = confirmPolicy;
            _uiManager = uiManager;
            _ct = ct;
            _bound = true;
            Refresh();
        }

        public void Refresh()
        {
            if (!_bound || _shop == null) return;

            ClearPool();

            var lots = _shop.GetLots(_storefrontId);
            if (lots == null || lots.Count == 0)
            {
                if (_emptyLabel != null)
                {
                    _emptyLabel.gameObject.SetActive(true);
                    _emptyLabel.text = "Nothing on offer today.";
                }
                return;
            }

            if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(false);

            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                if (lot == null) continue;
                var card = SpawnCard();
                if (card == null) continue;

                var capturedLotId = lot.LotId;
                card.Bind(lot, () => TryBuyAsync(capturedLotId).Forget());
                card.SetAvailable(_shop.IsAvailable(lot.LotId));
                card.gameObject.SetActive(true);
            }
        }

        private ShopLotCardView SpawnCard()
        {
            if (_cardTemplate == null || _cardContainer == null) return null;
            var card = Object.Instantiate(_cardTemplate, _cardContainer);
            _pool.Add(card);
            return card;
        }

        private void ClearPool()
        {
            for (var i = 0; i < _pool.Count; i++)
                if (_pool[i] != null) Object.Destroy(_pool[i].gameObject);
            _pool.Clear();
        }

        private async UniTaskVoid TryBuyAsync(string lotId)
        {
            if (_shop == null) return;
            if (!_shop.TryGetLot(lotId, out var lot)) return;

            if (_confirmPolicy != null && _confirmPolicy.RequiresConfirmation(lot))
            {
                var confirmed = await ShowConfirmAsync(lot);
                if (!confirmed) return;
            }

            var result = await _shop.BuyAsync(lotId, _ct);

            if (result.Status == ShopPurchaseStatus.Success && result.Granted != null
                && result.Granted.Items.Count > 0 && _uiManager != null)
            {
                await _uiManager.ShowAsync<RewardsWindow>(
                    new RewardsWindowArgs(result.Granted, $"Received from {lot.RewardId}"),
                    _ct);
            }
            else if (result.Status != ShopPurchaseStatus.Success)
            {
                Debug.Log($"[ShopLotsSectionView] Purchase '{lotId}' failed: {result.Status}.");
            }

            Refresh();
        }

        private async UniTask<bool> ShowConfirmAsync(ShopLot lot)
        {
            if (_uiManager == null) return true;

            var args = new ConfirmDialogArgs(
                title: $"Buy {lot.RewardId}?",
                body: $"Spend <b>{lot.Price.Amount} {lot.Price.Currency}</b> on this offer?",
                confirmLabel: "Buy",
                cancelLabel: "Cancel");

            var dialog = await _uiManager.ShowAsync<ConfirmDialog>(args, _ct);
            if (dialog == null) return false;

            var result = await dialog.WaitForResultAsync<ConfirmDialogResult>(_ct);
            return result == ConfirmDialogResult.Confirmed;
        }
    }
}
