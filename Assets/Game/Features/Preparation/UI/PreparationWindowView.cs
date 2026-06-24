using System;
using System.Collections.Generic;
using Game.Preparation.Domain;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Preparation.UI
{
    /// <summary>
    /// View окна Подготовки. Только ссылки на UI + простые сеттеры; вся логика — в PreparationWindow.
    /// </summary>
    public sealed class PreparationWindowView : WindowView
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _locationLabel;
        [SerializeField] private TMP_Text _slotCountLabel;

        [Header("Genre list")]
        [SerializeField] private Transform _genreListContainer;
        [SerializeField] private PreparationGenreRowView _genreRowPrefab;

        [Header("Shelf preview")]
        [SerializeField] private ShelfPreviewView _shelfPreview;

        [Header("Actions")]
        [SerializeField] private Button _openShopButton;
        [SerializeField] private Button _randomBooksButton;

        public Button OpenShopButton => _openShopButton;
        public Button RandomBooksButton => _randomBooksButton;
        public Transform GenreListContainer => _genreListContainer;
        public PreparationGenreRowView GenreRowPrefab => _genreRowPrefab;

        public void SetLocation(string value) => Set(_locationLabel, value);
        public void SetSlotCount(string value) => Set(_slotCountLabel, value);

        public void RenderShelfPreview(IReadOnlyList<GenreSelectionItem> items, Action<string> onSegmentClicked)
            => _shelfPreview?.Render(items, onSegmentClicked);

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
