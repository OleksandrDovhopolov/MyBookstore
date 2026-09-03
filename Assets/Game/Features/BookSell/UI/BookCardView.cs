using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Localization;
using SpriteService;
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
        [SerializeField] private TMP_Text _tagsLabel;     // optional, now renders qualities; prefab field name kept for compatibility

        [Header("Genre icon")]
        [Tooltip("Book genre sprite, loaded from Addressables by genre id. Optional.")]
        [SerializeField] private Image _genreImage;

        [Header("Interaction")]
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _selectedHighlight; // border/glow shown in the Selected state

        [Header("Visual states")]
        [Tooltip("Card opacity when sold out. 1 = opaque, 0.4 = typical dim.")]
        [SerializeField] [Range(0f, 1f)] private float _soldOutAlpha = 0.4f;
        [SerializeField] private CanvasGroup _canvasGroup;      // auto-resolved in Awake if left unassigned

        private Action<string> _onClicked;
        private CancellationTokenSource _genreIconCts;
        public string BookId { get; private set; }

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_button != null) _button.onClick.AddListener(OnButtonClicked);
        }

        public void Bind(BookConfig book, Action<string> onClicked, IUiSpriteProvider sprites)
        {
            BookId = book.Id;
            _onClicked = onClicked;

            Set(_titleLabel, LocalizationLocator.GetOrKey(book.TitleKey));
            Set(_authorLabel, LocalizationLocator.GetOrKey(book.AuthorKey));
            Set(_genreLabel, book.PrimaryGenre);
            Set(_priceLabel, BookConfig.FixedPriceGold.ToString());

            if (_tagsLabel != null)
            {
                var qualities = book.Qualities ?? Array.Empty<string>();
                _tagsLabel.text = qualities.Length > 0 ? string.Join(", ", qualities) : "";
            }

            LoadGenreIcon(book.PrimaryGenre, sprites);

            SetSelected(false);
            SetSoldOut(false);
        }

        // Loads the book's genre sprite from Addressables by genre id (same resolution as the customer
        // thought bubble: parse the config genre to the BookGenre enum and use its name as the sprite id).
        private void LoadGenreIcon(string genre, IUiSpriteProvider sprites)
        {
            CancelGenreIconLoad();
            if (_genreImage == null) return;

            if (sprites == null || !BookGenreExtensions.TryParseGenre(genre, out var parsed))
            {
                _genreImage.sprite = null;
                return;
            }

            _genreIconCts = new CancellationTokenSource();
            LoadGenreIconAsync(parsed.ToString(), sprites, _genreIconCts.Token).Forget();
        }

        private async UniTaskVoid LoadGenreIconAsync(string genreId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(genreId, ct);
                if (ct.IsCancellationRequested || _genreImage == null) return;
                _genreImage.sprite = sprite;
            }
            catch (OperationCanceledException)
            {
                // card destroyed / rebound mid-load — ignore
            }
        }

        private void CancelGenreIconLoad()
        {
            if (_genreIconCts == null) return;
            _genreIconCts.Cancel();
            _genreIconCts.Dispose();
            _genreIconCts = null;
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
            CancelGenreIconLoad();
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
