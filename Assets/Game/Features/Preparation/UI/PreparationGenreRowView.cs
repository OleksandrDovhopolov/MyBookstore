using System;
using Game.Configs.Models;
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
        [Tooltip("Сколько книг этого жанра осталось в инвентаре (Available − на полке).")]
        [SerializeField] private TMP_Text _inventoryCountLabel;
        [Tooltip("Сколько книг этого жанра выставлено на полку.")]
        [SerializeField] private TMP_Text _shelfCountLabel;
        [SerializeField] private Button _minusButton;
        [SerializeField] private Button _plusButton;
        [Tooltip("Источник фоновых спрайтов кнопок −/+ по жанру.")]
        [SerializeField] private GenrePalette _palette;
        [SerializeField] private Image _iconImage;

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
            ApplyButtonBackground();
            Refresh();
        }

        // Уникальный фон кнопок −/+ под жанр (из палитры). Общая для жанра подложка на обе кнопки.
        private void ApplyButtonBackground()
        {
            if (_palette == null || !BookGenreExtensions.TryParseGenre(_genre, out var genre))
                return;

            var background = _palette.GetButtonBackground(genre);
            if (background == null) return;

            if (_minusButton != null && _minusButton.image != null) _minusButton.image.sprite = background;
            if (_plusButton != null && _plusButton.image != null) _plusButton.image.sprite = background;
        }

        /// <summary>Иконка жанра. Спрайт грузит контроллер по id жанра (Addressables) и передаёт сюда.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (_iconImage != null) _iconImage.sprite = sprite;
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

            if (_inventoryCountLabel != null) _inventoryCountLabel.text = "in storage : " + inInventory;
            if (_shelfCountLabel != null) _shelfCountLabel.text = "" + _quantity;
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
