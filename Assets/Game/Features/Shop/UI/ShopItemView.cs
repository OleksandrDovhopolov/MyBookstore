using System;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shop.UI
{
    public sealed class ShopItemView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private GameObject _soldRoot;
        [SerializeField] private GameObject _priceRoot;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Button _decorInfoButton;

        private Action _onBuyClicked;
        private Action<string> _onDecorInfoClicked;
        private bool _isDecor;

        public string LotId { get; private set; }
        public string IconId { get; private set; }

        public void Bind(
            ShopOffer offer,
            Action onBuyClicked,
            Sprite icon = null,
            Action<string> onDecorInfoClicked = null)
        {
            if (offer == null) return;

            LotId = offer.LotId;
            IconId = offer.IconId;
            _isDecor = offer.IsDecor;
            _onBuyClicked = onBuyClicked;
            _onDecorInfoClicked = onDecorInfoClicked;

            SetIcon(icon);
            UpdateOfferState(offer);

            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveListener(OnBuyClickedInternal);
                _buyButton.onClick.AddListener(OnBuyClickedInternal);
            }

            if (_decorInfoButton != null)
            {
                _decorInfoButton.onClick.RemoveListener(OnDecorInfoClickedInternal);
                _decorInfoButton.onClick.AddListener(OnDecorInfoClickedInternal);
            }
        }

        public void UpdateOfferState(ShopOffer offer)
        {
            if (offer == null) return;

            if (_priceLabel != null) _priceLabel.text = offer.PriceText;
            SetSoldVisible(offer.IsDecor && !offer.IsAvailable);

            if (_buyButton != null)
                _buyButton.interactable = offer.IsAvailable;

            if (_decorInfoButton != null)
                _decorInfoButton.interactable = offer.IsDecor && _onDecorInfoClicked != null;
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null)
                _icon.sprite = sprite;
        }

        private void OnBuyClickedInternal() => _onBuyClicked?.Invoke();

        private void OnDecorInfoClickedInternal()
        {
            if (!_isDecor || string.IsNullOrEmpty(IconId)) return;
            _onDecorInfoClicked?.Invoke(IconId);
        }

        private void SetSoldVisible(bool visible)
        {
            if (_soldRoot != null && _soldRoot != gameObject)
                _soldRoot.SetActive(visible);
            if (_priceRoot != null && _priceRoot != gameObject)
                _priceRoot.SetActive(!visible);
        }

        public void Cleanup()
        {
            if (_buyButton != null)
                _buyButton.onClick.RemoveListener(OnBuyClickedInternal);
            if (_decorInfoButton != null)
            {
                _decorInfoButton.onClick.RemoveListener(OnDecorInfoClickedInternal);
                _decorInfoButton.interactable = false;
            }

            _onBuyClicked = null;
            _onDecorInfoClicked = null;
            _isDecor = false;
            LotId = null;
            IconId = null;
            SetIcon(null);
            if (_priceLabel != null) _priceLabel.text = string.Empty;
            SetSoldVisible(false);
        }

        private void OnDestroy() => Cleanup();
    }
}
