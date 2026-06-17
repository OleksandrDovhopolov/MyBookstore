using System;
using Game.Shop.API;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shop.UI.Sections
{
    /// <summary>
    /// Single lot card rendered inside a <see cref="ShopLotsSectionView"/>. Phase 1 MVP layout:
    /// name (rewardId placeholder), price ("N gold"), buy button. Icons + per-category visuals come
    /// in Phase 1.5/2 when real art is hooked up.
    /// </summary>
    public sealed class ShopLotCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private Button _buyButton;
        [SerializeField] private CanvasGroup _canvasGroup;

        private Action _onBuyClicked;

        public void Bind(ShopLot lot, Action onBuyClicked)
        {
            if (lot == null) return;

            if (_nameLabel != null)
                _nameLabel.text = lot.RewardId;

            if (_priceLabel != null)
                _priceLabel.text = $"{lot.Price.Amount} {lot.Price.Currency}";

            _onBuyClicked = onBuyClicked;
            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveAllListeners();
                _buyButton.onClick.AddListener(OnBuyClickedInternal);
            }
        }

        public void SetAvailable(bool available)
        {
            if (_buyButton != null) _buyButton.interactable = available;
            if (_canvasGroup != null) _canvasGroup.alpha = available ? 1f : 0.5f;
        }

        private void OnBuyClickedInternal() => _onBuyClicked?.Invoke();

        private void OnDestroy()
        {
            if (_buyButton != null) _buyButton.onClick.RemoveAllListeners();
            _onBuyClicked = null;
        }
    }
}
