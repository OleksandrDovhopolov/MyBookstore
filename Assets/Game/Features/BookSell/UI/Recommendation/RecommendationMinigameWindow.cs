using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.UI;
using TMPro;
using UnityEngine;

namespace Book.Sell.UI
{
    /// <summary>
    /// Modal window for the active recommendation minigame (extracted from <see cref="SalesScreenView"/>).
    /// Shows the customer's request + the current shelf; clicking a book opens a detail panel; Recommend/Skip
    /// resolve the request via <see cref="ISalesDayController"/>; the resolution swaps in a result container
    /// showing the customer's reaction (mapped from <see cref="RecommendationTier"/>) and a Finish button that
    /// closes the window. The day is paused by <see cref="SalesScreenView"/> while this window is open, so the
    /// shelf is static and rendered once on show.
    /// </summary>
    [Window("RecommendationMinigameWindow", WindowType.Popup)]
    public sealed class RecommendationMinigameWindow : WindowController<RecommendationMinigameWindowView>
    {
        private const string TodoPlaceholder = "TODO";

        private ISalesDayController _controller;
        private readonly List<BookCardView> _cards = new();
        private string _selectedBookId;
        private bool _subscribed;

        protected override void OnInit()
        {
            if (View.RecommendButton != null) View.RecommendButton.onClick.AddListener(OnRecommend);
            if (View.SkipButton != null) View.SkipButton.onClick.AddListener(OnSkip);
            if (View.FinishButton != null) View.FinishButton.onClick.AddListener(OnFinish);
        }

        protected override void OnShowStart()
        {
            _controller = (Arguments as RecommendationMinigameArgs)?.Controller;
            if (_controller == null)
            {
                Debug.LogError("[RecommendationMinigameWindow] No ISalesDayController in args — cannot run the minigame.");
                return;
            }

            // Selection state visible, result hidden.
            if (View.MinigameRoot != null) View.MinigameRoot.SetActive(true);
            if (View.ResultPanel != null) View.ResultPanel.SetActive(false);
            if (View.DetailPanel != null) View.DetailPanel.SetActive(false);

            RenderRequest(_controller.CurrentRequest);
            PopulateShelfCards();
            ClearSelection();

            Subscribe();
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            Unsubscribe();
        }

        protected override void OnDispose()
        {
            Unsubscribe();
            ClearCards();

            if (View != null)
            {
                if (View.RecommendButton != null) View.RecommendButton.onClick.RemoveListener(OnRecommend);
                if (View.SkipButton != null) View.SkipButton.onClick.RemoveListener(OnSkip);
                if (View.FinishButton != null) View.FinishButton.onClick.RemoveListener(OnFinish);
            }

            _controller = null;
        }

        private void Subscribe()
        {
            if (_subscribed || _controller == null) return;
            _controller.RecommendationResolved += OnResolved;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _controller == null) return;
            _controller.RecommendationResolved -= OnResolved;
            _subscribed = false;
        }

        // ---------- request + shelf ----------

        private void RenderRequest(RequestConfig request)
        {
            Set(View.RequestText, request?.Text);
            if (View.DifficultyLabel != null)
            {
                var difficulty = request?.Difficulty ?? RequestDifficulty.Unknown;
                View.DifficultyLabel.text = difficulty == RequestDifficulty.Unknown
                    ? string.Empty
                    : $"Difficulty: {(int)difficulty}/5";
            }
        }

        private void PopulateShelfCards()
        {
            ClearCards();
            if (View.BookCardPrefab == null || View.ShelfContainer == null) return;

            var shelf = _controller.Shelf;
            foreach (var shelfBook in shelf.Books)
            {
                var card = Object.Instantiate(View.BookCardPrefab, View.ShelfContainer);
                card.Bind(shelfBook.Config, OnBookCardClicked);

                var available = shelfBook.State == ShelfBookState.Available && !shelf.IsReserved(shelfBook.BookId);
                card.SetSoldOut(!available);

                _cards.Add(card);
            }
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
                if (card != null) Object.Destroy(card.gameObject);
            _cards.Clear();
        }

        // ---------- selection + detail ----------

        private void OnBookCardClicked(string bookId)
        {
            _selectedBookId = bookId;
            foreach (var card in _cards) card.SetSelected(card.BookId == bookId);

            ShowDetail(_controller.Shelf.Find(bookId)?.Config);
            if (View.RecommendButton != null) View.RecommendButton.interactable = true;
        }

        private void ShowDetail(BookConfig book)
        {
            if (View.DetailPanel != null) View.DetailPanel.SetActive(book != null);
            if (book == null) return;

            Set(View.DetailTitle, book.Title);
            Set(View.DetailAuthor, book.Author);

            // BookConfig has no description / publish date / page count yet — placeholder until added.
            Set(View.DetailDescription, TodoPlaceholder);
            Set(View.DetailPublishDate, TodoPlaceholder);
            Set(View.DetailPageCount, TodoPlaceholder);
        }

        private void ClearSelection()
        {
            _selectedBookId = null;
            foreach (var card in _cards) card.SetSelected(false);
            if (View.DetailPanel != null) View.DetailPanel.SetActive(false);
            if (View.RecommendButton != null) View.RecommendButton.interactable = false;
        }

        // ---------- actions ----------

        private void OnRecommend()
        {
            if (_controller == null || string.IsNullOrEmpty(_selectedBookId)) return;
            _controller.RecommendBook(_selectedBookId);
        }

        private void OnSkip()
        {
            if (_controller == null) return;
            _controller.SkipCurrentRequest();
        }

        private void OnFinish() => CloseAsync().Forget();

        // ---------- resolution ----------

        private void OnResolved(RecommendationResult result)
        {
            if (View.MinigameRoot != null) View.MinigameRoot.SetActive(false);
            if (View.ResultPanel != null) View.ResultPanel.SetActive(true);

            Set(View.EmotionLabel, EmotionFor(result?.Tier ?? RecommendationTier.Skipped));
            if (View.FinishButton != null) View.FinishButton.interactable = true;
        }

        // TODO: replace with proper reaction art/animation (hearts, speech bubble, etc.).
        private static string EmotionFor(RecommendationTier tier) => tier switch
        {
            RecommendationTier.Excellent => "Woohoo! Sci-fi at its best!",
            RecommendationTier.Normal => "Thanks, I'll take it!",
            RecommendationTier.Failed => "Hmm, that's not what I wanted...",
            RecommendationTier.Skipped => "Maybe next time.",
            _ => string.Empty
        };

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
