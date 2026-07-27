using System;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Newspaper.UI
{
    public sealed class NewspaperOfferCardView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private GameObject _soldRoot;
        [SerializeField] private GameObject _priceRoot;
        [SerializeField] private Button _buyButton;

        private Action _onBuyClicked;

        public string LotId { get; private set; }
        public string IconId { get; private set; }

        public void Bind(NewspaperOffer offer, Action onBuyClicked, Sprite icon = null)
        {
            if (offer == null) return;

            LotId = offer.LotId;
            IconId = offer.IconId;
            SetIcon(icon);
            UpdateOfferState(offer);

            _onBuyClicked = onBuyClicked;
            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveListener(OnBuyClickedInternal);
                _buyButton.onClick.AddListener(OnBuyClickedInternal);
            }
        }

        public void UpdateOfferState(NewspaperOffer offer)
        {
            if (offer == null) return;

            if (_priceLabel != null) _priceLabel.text = offer.PriceText;
            SetSoldVisible(offer.IsDecor && !offer.IsAvailable);

            if (_buyButton != null)
                _buyButton.interactable = offer.IsAvailable;
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null)
                _icon.sprite = sprite;
        }

        private void OnBuyClickedInternal() => _onBuyClicked?.Invoke();

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

            _onBuyClicked = null;
            LotId = null;
            IconId = null;
            SetIcon(null);
            if (_priceLabel != null) _priceLabel.text = string.Empty;
            SetSoldVisible(false);
        }

        private void OnDestroy() => Cleanup();
    }
}
