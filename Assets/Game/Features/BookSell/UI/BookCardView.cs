using System;
using Game.Configs.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI
{
    /// <summary>
    /// A single book card inside the shelf grid. <see cref="SalesScreenView"/> instantiates one
    /// per <see cref="Domain.ShelfBook"/>. States: Available (clickable) / Selected (highlighted)
    /// / SoldOut (dimmed and non-interactable).
    /// </summary>
    public sealed class BookCardView : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _authorLabel;
        [SerializeField] private TMP_Text _genreLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private TMP_Text _tagsLabel;     // optional, can be left unassigned

        [Header("Interaction")]
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _selectedHighlight; // border/glow shown in the Selected state

        [Header("Visual states")]
        [Tooltip("Card opacity when sold out. 1 = opaque, 0.4 = typical dim.")]
        [SerializeField] [Range(0f, 1f)] private float _soldOutAlpha = 0.4f;
        [SerializeField] private CanvasGroup _canvasGroup;      // auto-resolved in Awake if left unassigned

        private Action<string> _onClicked;
        public string BookId { get; private set; }

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_button != null) _button.onClick.AddListener(OnButtonClicked);
        }

        public void Bind(BookConfig book, Action<string> onClicked)
        {
            BookId = book.Id;
            _onClicked = onClicked;

            Set(_titleLabel, book.Title);
            Set(_authorLabel, book.Author);
            Set(_genreLabel, book.Genre);
            Set(_priceLabel, book.BasePrice.ToString());

            if (_tagsLabel != null)
            {
                var tags = book.Tags ?? Array.Empty<string>();
                _tagsLabel.text = tags.Length > 0 ? string.Join(", ", tags) : "";
            }

            SetSelected(false);
            SetSoldOut(false);
        }

        public void SetSelected(bool selected)
        {
            if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);
        }

        public void SetSoldOut(bool soldOut)
        {
            if (_button != null) _button.interactable = !soldOut;
            if (_canvasGroup != null) _canvasGroup.alpha = soldOut ? _soldOutAlpha : 1f;
        }

        private void OnButtonClicked() => _onClicked?.Invoke(BookId);

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnButtonClicked);
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
