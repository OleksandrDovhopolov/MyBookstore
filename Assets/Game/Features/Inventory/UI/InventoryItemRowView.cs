using System;
using Game.Configs.Models;
using Game.Inventory.API;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory.UI
{
    /// <summary>
    /// Single row in the debug inventory list. Bound by <see cref="InventoryScreenView"/>; one
    /// instantiated per item. Book rows can optionally render the full <c>BookConfig</c> through
    /// <see cref="BindBook"/> — other categories use the plain <see cref="Bind"/>.
    /// </summary>
    public sealed class InventoryItemRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _idLabel;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private Button _useButton;
        [SerializeField] private TextMeshProUGUI _index;

        [Header("Book details (optional — only filled for book category)")]
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _authorLabel;
        [SerializeField] private TMP_Text _genreLabel;
        [SerializeField] private TMP_Text _basePriceLabel;
        [SerializeField] private TMP_Text _rarityWeightLabel;
        [SerializeField] private TMP_Text _tagsLabel;
        [SerializeField] private TMP_Text _moodLabel;

        private string _itemId;
        private Action<string> _onUse;

        private void Awake()
        {
            if (_useButton != null) _useButton.onClick.AddListener(OnUseClicked);
        }

        public void Bind(InventoryItem item, bool hasUseHandler, string info, Action<string> onUse, int index)
        {
            _itemId = item.ItemId;
            _onUse = onUse;

            if (_index != null) _index.text = $"×{index}";

            if (_idLabel != null)
                _idLabel.text = string.IsNullOrEmpty(info) ? item.ItemId : $"{item.ItemId} — {info}";
            if (_countLabel != null)
            {
                _countLabel.gameObject.SetActive(item.Count > 1);
                _countLabel.text = $"×{item.Count}";
            }

            ClearBookDetailLabels();
        }

        /// <summary>
        /// Bind with full <see cref="BookConfig"/> display. Falls back to <see cref="Bind"/> behavior
        /// for the common fields, then fills the book-specific labels (any null SerializeField is
        /// silently skipped — prefab decides which fields to show).
        /// </summary>
        public void BindBook(InventoryItem item, BookConfig book, bool hasUseHandler, Action<string> onUse, int index)
        {
            Bind(item, hasUseHandler, info: null, onUse, index);
            if (book == null) return;

            if (_titleLabel != null) _titleLabel.text = book.Title ?? string.Empty;
            if (_authorLabel != null) _authorLabel.text = book.Author ?? string.Empty;
            if (_genreLabel != null) _genreLabel.text = book.Genre ?? string.Empty;
            if (_basePriceLabel != null) _basePriceLabel.text = $"{book.BasePrice} gold";
            if (_rarityWeightLabel != null) _rarityWeightLabel.text = $"R: {book.RarityWeight:F2}";
            if (_tagsLabel != null)
                _tagsLabel.text = book.Tags != null && book.Tags.Length > 0
                    ? string.Join(", ", book.Tags)
                    : string.Empty;
            if (_moodLabel != null)
                _moodLabel.text = book.Mood != null && book.Mood.Length > 0
                    ? string.Join(", ", book.Mood)
                    : string.Empty;
        }

        private void ClearBookDetailLabels()
        {
            if (_titleLabel != null) _titleLabel.text = string.Empty;
            if (_authorLabel != null) _authorLabel.text = string.Empty;
            if (_genreLabel != null) _genreLabel.text = string.Empty;
            if (_basePriceLabel != null) _basePriceLabel.text = string.Empty;
            if (_rarityWeightLabel != null) _rarityWeightLabel.text = string.Empty;
            if (_tagsLabel != null) _tagsLabel.text = string.Empty;
            if (_moodLabel != null) _moodLabel.text = string.Empty;
        }

        private void OnUseClicked()
        {
            if (!string.IsNullOrEmpty(_itemId)) _onUse?.Invoke(_itemId);
        }

        private void OnDestroy()
        {
            if (_useButton != null) _useButton.onClick.RemoveListener(OnUseClicked);
        }
    }
}
