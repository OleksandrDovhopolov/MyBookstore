using System;
using Game.Preparation.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Preparation.UI
{
    /// <summary>
    /// Одна строка выбора по жанру: название, счётчик «quantity/available» и кнопки −/+.
    /// PreparationWindow инстансит префаб на каждый GenreSelectionItem.
    /// </summary>
    public sealed class PreparationGenreRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _genreLabel;
        [Tooltip("Опционально: совмещённый счётчик «на полке/всего» ({quantity}/{available}).")]
        [SerializeField] private TMP_Text _countLabel;
        [Tooltip("Сколько книг этого жанра осталось в инвентаре (Available − на полке).")]
        [SerializeField] private TMP_Text _inventoryCountLabel;
        [Tooltip("Сколько книг этого жанра выставлено на полку.")]
        [SerializeField] private TMP_Text _shelfCountLabel;
        [SerializeField] private Button _minusButton;
        [SerializeField] private Button _plusButton;

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

            if (_genreLabel != null) _genreLabel.text = item.Genre;
            Refresh();
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
            // В инвентаре остаётся всё непроданное минус то, что уже на полке.
            var inInventory = Mathf.Max(0, _available - _quantity);

            if (_countLabel != null) _countLabel.text = $"{_quantity}/{_available}";
            if (_inventoryCountLabel != null) _inventoryCountLabel.text = "Inventory - " + inInventory;
            if (_shelfCountLabel != null) _shelfCountLabel.text = "Shelf - " + _quantity;
            if (_minusButton != null) _minusButton.interactable = _quantity > 0;
            if (_plusButton != null) _plusButton.interactable = _quantity < _available && _canAddMore;
        }

        private void OnMinus() => _onSetQuantity?.Invoke(_genre, _quantity - 1);
        private void OnPlus() => _onSetQuantity?.Invoke(_genre, _quantity + 1);

        private void OnDestroy()
        {
            if (_minusButton != null) _minusButton.onClick.RemoveListener(OnMinus);
            if (_plusButton != null) _plusButton.onClick.RemoveListener(OnPlus);
        }
    }
}
