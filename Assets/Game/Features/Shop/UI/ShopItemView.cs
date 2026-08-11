using System;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Shop.UI
{
    public sealed class ShopItemView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _bookIcon;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private GameObject _soldRoot;
        [SerializeField] private GameObject _priceRoot;
        [SerializeField] private Button _buyButton;
        [FormerlySerializedAs("_decorInfoButton")]
        [SerializeField] private Button _infoButton;

        private Action _onBuyClicked;
        private Action<string, RectTransform> _onInfoClicked;

        public string LotId { get; private set; }
        public string IconId { get; private set; }
        public string BookIconId { get; private set; }

        public void Bind(
            ShopOffer offer,
            Action onBuyClicked,
            Sprite icon = null,
            Action<string, RectTransform> onInfoClicked = null)
        {
            if (offer == null) return;

            LotId = offer.LotId;
            IconId = offer.IconId;
            BookIconId = offer.BookIconId;
            _onBuyClicked = onBuyClicked;
            _onInfoClicked = onInfoClicked;

            SetIcon(icon);
            SetBookIcon(null);
            UpdateOfferState(offer);

            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveListener(OnBuyClickedInternal);
                _buyButton.onClick.AddListener(OnBuyClickedInternal);
            }

            if (_infoButton != null)
            {
                _infoButton.onClick.RemoveListener(OnInfoClickedInternal);
                _infoButton.onClick.AddListener(OnInfoClickedInternal);
            }
        }

        public void UpdateOfferState(ShopOffer offer)
        {
            if (offer == null) return;

            if (_priceLabel != null) _priceLabel.text = offer.PriceText;
            SetSoldVisible(!offer.IsAvailable);

            if (_buyButton != null)
                _buyButton.interactable = offer.IsAvailable;

            if (_infoButton != null)
                _infoButton.interactable = _onInfoClicked != null;
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null)
                _icon.sprite = sprite;
        }

        public void SetBookIcon(Sprite sprite)
        {
            if (_bookIcon == null) return;

            _bookIcon.sprite = sprite;
            _bookIcon.enabled = sprite != null;
        }

        private void OnBuyClickedInternal() => _onBuyClicked?.Invoke();

        private void OnInfoClickedInternal()
        {
            if (string.IsNullOrEmpty(LotId)) return;

            var anchor = _infoButton != null && _infoButton.transform is RectTransform buttonRect
                ? buttonRect
                : transform as RectTransform;
            _onInfoClicked?.Invoke(LotId, anchor);
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
            if (_infoButton != null)
            {
                _infoButton.onClick.RemoveListener(OnInfoClickedInternal);
                _infoButton.interactable = false;
            }

            _onBuyClicked = null;
            _onInfoClicked = null;
            LotId = null;
            IconId = null;
            BookIconId = null;
            SetIcon(null);
            SetBookIcon(null);
            if (_priceLabel != null) _priceLabel.text = string.Empty;
            SetSoldVisible(false);
        }

        private void OnDestroy() => Cleanup();
    }
}
