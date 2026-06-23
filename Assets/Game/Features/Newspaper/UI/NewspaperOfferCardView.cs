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
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private GameObject _stateRoot;
        [SerializeField] private TMP_Text _stateLabel;
        [SerializeField] private Button _buyButton;
        [SerializeField] private CanvasGroup _canvasGroup;

        private Action _onBuyClicked;

        public string LotId { get; private set; }
        public Button BuyButton => _buyButton;
        public Image Icon => _icon;

        public void Bind(NewspaperOffer offer, Action onBuyClicked, Sprite icon = null)
        {
            if (offer == null) return;

            LotId = offer.LotId;
            SetIcon(icon);
            if (_titleLabel != null) _titleLabel.text = offer.DisplayName;
            if (_descriptionLabel != null) _descriptionLabel.text = offer.Description;
            if (_priceLabel != null) _priceLabel.text = offer.PriceText;

            var hasState = !string.IsNullOrEmpty(offer.StateText);
            if (_stateRoot != null) _stateRoot.SetActive(hasState);
            if (_stateLabel != null) _stateLabel.text = offer.StateText ?? string.Empty;

            _onBuyClicked = onBuyClicked;
            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveListener(OnBuyClickedInternal);
                _buyButton.onClick.AddListener(OnBuyClickedInternal);
                _buyButton.interactable = offer.IsAvailable;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = offer.IsAvailable ? 1f : 0.65f;
                _canvasGroup.interactable = offer.IsAvailable;
                _canvasGroup.blocksRaycasts = offer.IsAvailable;
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null)
                _icon.sprite = sprite;
        }

        private void OnBuyClickedInternal() => _onBuyClicked?.Invoke();

        public void Cleanup()
        {
            if (_buyButton != null)
                _buyButton.onClick.RemoveListener(OnBuyClickedInternal);

            _onBuyClicked = null;
            LotId = null;
            SetIcon(null);
        }

        private void OnDestroy() => Cleanup();
    }
}
