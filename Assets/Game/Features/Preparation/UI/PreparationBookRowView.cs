using System;
using Game.Preparation.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Preparation.UI
{
    /// <summary>
    /// Одна строка списка выбора книги. Префаб с TMP-полями и кнопкой-тогглом.
    /// PreparationWindow инстансит префаб на каждую SelectableBookItem и вызывает Bind.
    /// </summary>
    public sealed class PreparationBookRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _genreLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private GameObject _selectedMarker;

        private string _bookId;
        private Action<string> _onClick;

        public string BookId => _bookId;

        private void Awake()
        {
            if (_toggleButton != null)
                _toggleButton.onClick.AddListener(OnClicked);
        }

        public void Bind(SelectableBookItem item, Action<string> onClick)
        {
            _bookId = item.BookId;
            _onClick = onClick;

            if (_titleLabel != null) _titleLabel.text = item.Title;
            if (_genreLabel != null) _genreLabel.text = item.Genre;
            if (_priceLabel != null) _priceLabel.text = $"{item.BasePrice}";
            SetSelected(item.IsSelected);
        }

        public void SetSelected(bool isSelected)
        {
            if (_selectedMarker != null)
                _selectedMarker.SetActive(isSelected);
        }

        private void OnClicked()
        {
            if (string.IsNullOrEmpty(_bookId)) return;
            _onClick?.Invoke(_bookId);
        }

        private void OnDestroy()
        {
            if (_toggleButton != null)
                _toggleButton.onClick.RemoveListener(OnClicked);
        }
    }
}
