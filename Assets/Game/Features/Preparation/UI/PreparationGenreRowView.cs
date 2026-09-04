using System;
using Game.Preparation.Domain;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Preparation.UI
{
    public sealed class PreparationGenreRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private TMP_Text _inventoryCountLabel;
        [SerializeField] private Image _inventoryIconImage;
        [SerializeField] private Button _minusButton;
        [SerializeField] private Button _plusButton;
        [SerializeField] private TMP_Text _shelfCountLabel;
        [SerializeField] private Image _shelfIconImage;

        private string _genre;
        private int _available;
        private int _quantity;
        private bool _canAddMore = true;
        private Action<string, int> _onSetQuantity;

        public string Genre => _genre;

        private void Awake()
        {
            if (_minusButton != null) _minusButton.onClick.AddListener(OnMinus);
            if (_plusButton != null) _plusButton.onClick.AddListener(OnPlus);
        }

        public void Bind(GenreSelectionItem item, Action<string, int> onSetQuantity)
        {
            _genre = item.Genre;
            _available = item.Available;
            _quantity = item.Quantity;
            _onSetQuantity = onSetQuantity;

            Refresh();
        }

        /// <summary>Иконка жанра. Спрайт грузит контроллер по id жанра (Addressables) и передаёт сюда.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (_inventoryIconImage != null) _inventoryIconImage.sprite = sprite;
            if (_shelfIconImage != null) _shelfIconImage.sprite = sprite;
        }

        /// <param name="canAddMore">false, когда общий лимит полки уже достигнут.</param>
        public void SetState(int quantity, bool canAddMore)
        {
            _quantity = Mathf.Clamp(quantity, 0, _available);
            _canAddMore = canAddMore;
            Refresh();
        }

        private void Refresh()
        {
            // "In stock" shows what is still available to shelf = owned minus what is already on the shelf,
            // so the number drops as the player adds copies and returns as they remove them. _available stays
            // the owned ceiling used for clamping; the shelf label shows the selected count. The two always
            // sum to owned. Nothing here touches real inventory — this is preparation-session math only.
            if (_inventoryCountLabel != null) _inventoryCountLabel.text = Mathf.Max(0, _available - _quantity).ToString();
            if (_shelfCountLabel != null) _shelfCountLabel.text = _quantity.ToString();
            if (_minusButton != null) _minusButton.interactable = _quantity > 0;
            if (_plusButton != null) _plusButton.interactable = _quantity < _available && _canAddMore;
        }

        private void OnMinus() => _onSetQuantity?.Invoke(_genre, _quantity - 1);
        private void OnPlus() => _onSetQuantity?.Invoke(_genre, _quantity + 1);

        public void Cleanup()
        {
            _genre = null;
            _available = 0;
            _quantity = 0;
            _canAddMore = true;
            _onSetQuantity = null;

            if (_inventoryCountLabel != null) _inventoryCountLabel.text = string.Empty;
            if (_shelfCountLabel != null) _shelfCountLabel.text = string.Empty;
            if (_inventoryIconImage != null) _inventoryIconImage.sprite = null;
            if (_shelfIconImage != null) _shelfIconImage.sprite = null;
            if (_minusButton != null) _minusButton.interactable = false;
            if (_plusButton != null) _plusButton.interactable = false;
        }

        private void OnDestroy()
        {
            if (_minusButton != null) _minusButton.onClick.RemoveListener(OnMinus);
            if (_plusButton != null) _plusButton.onClick.RemoveListener(OnPlus);
        }
    }
}
