using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.UI;
using SpriteService;
using TMPro;
using UnityEngine;
using VContainer;

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
    [Window("RecommendationMinigameWindow", WindowType.Popup, true)]
    public sealed class RecommendationMinigameWindow : WindowController<RecommendationMinigameWindowView>
    {
        private ISalesDayController _controller;
        private IUiSpriteProvider _uiSprites;
        private readonly List<BookCardView> _cards = new();
        private string _selectedBookId;
        private bool _subscribed;

        // Resolved from the bootstrap scope (global singleton), same as DialogWindow injects IConfigsService.
        // Null-safe: BookCardView.Bind skips the icon load when the provider is unavailable.
        [Inject]
        public void InjectSprites(IUiSpriteProvider uiSprites) => _uiSprites = uiSprites;

        protected override void OnInit()
        {
            if (View.RecommendButton != null) View.RecommendButton.onClick.AddListener(OnRecommend);
            if (View.SkipButton != null) View.SkipButton.onClick.AddListener(OnSkip);
            if (View.ClearFocusButton != null) View.ClearFocusButton.onClick.AddListener(OnClearFocus);
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
            if (View.Animator != null)
                View.Animator.PrepareForRequest();
            else
            {
                if (View.ResultPanel != null) View.ResultPanel.SetActive(false);
                if (View.DetailPanel != null) View.DetailPanel.SetActive(false);
            }

            RenderRequest(_controller.CurrentRequest);
            PopulateShelfCards();
            ClearSelection(instant: true);
            View.Animator?.PlayRequestIntro();

            Subscribe();
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            View?.Animator?.HideSelection();
            Unsubscribe();
        }

        protected override void OnDispose()
        {
            Unsubscribe();
            View?.Animator?.KillAll();
            ClearCards();

            if (View != null)
            {
                if (View.RecommendButton != null) View.RecommendButton.onClick.RemoveListener(OnRecommend);
                if (View.SkipButton != null) View.SkipButton.onClick.RemoveListener(OnSkip);
                if (View.ClearFocusButton != null) View.ClearFocusButton.onClick.RemoveListener(OnClearFocus);
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

        private void RenderRequest(ActiveRequestRuntime request)
        {
            Set(View.RequestText, request?.Text);
        }

        private void PopulateShelfCards()
        {
            ClearCards();
            if (View.BookCardPrefab == null || View.ShelfContainer == null) return;

            var shelf = _controller.Shelf;
            foreach (var shelfBook in shelf.Books)
            {
                var card = Object.Instantiate(View.BookCardPrefab, View.ShelfContainer);
                card.Bind(shelfBook.Config, OnBookCardClicked, _uiSprites);

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

            Set(View.DetailDescription, book.Description);
            Set(View.DetailPublishDate, book.Published.ToString());
            Set(View.DetailPageCount, book.Pages.ToString());

            View.Animator?.ShowBookDetail();
        }

        private void ClearSelection(bool instant = false)
        {
            _selectedBookId = null;
            foreach (var card in _cards) card.SetSelected(false);

            if (View.Animator != null)
            {
                if (instant)
                {
                    View.Animator.HideBookDetailInstant();
                    ClearDetailAfterHide();
                }
                else
                {
                    View.Animator.HideBookDetail(ClearDetailAfterHide);
                }
            }
            else
            {
                if (View.DetailPanel != null) View.DetailPanel.SetActive(false);
                ClearDetailText();
            }

            if (View.RecommendButton != null) View.RecommendButton.interactable = false;
        }

        private void ClearDetailText()
        {
            Set(View.DetailTitle, string.Empty);
            Set(View.DetailAuthor, string.Empty);
            Set(View.DetailDescription, string.Empty);
            Set(View.DetailPublishDate, string.Empty);
            Set(View.DetailPageCount, string.Empty);
        }

        private void ClearDetailAfterHide()
        {
            if (View.DetailPanel != null) View.DetailPanel.SetActive(false);
            ClearDetailText();
        }

        // ---------- actions ----------

        private void OnClearFocus() => ClearSelection();

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
            var emotion = EmotionFor(result?.Tier ?? RecommendationTier.Skipped);
            Set(View.EmotionLabel, emotion);
            if (View.Animator != null)
            {
                View.Animator.HideSelection(onComplete: () =>
                {
                    if (View.MinigameRoot != null) View.MinigameRoot.SetActive(false);
                    View.Animator.PlayResult(emotion);
                });
            }
            else
            {
                if (View.MinigameRoot != null) View.MinigameRoot.SetActive(false);
                if (View.ResultPanel != null) View.ResultPanel.SetActive(true);
                Set(View.EmotionLabel, emotion);
                if (View.FinishButton != null) View.FinishButton.interactable = true;
            }
        }

        // TODO: replace with proper reaction art/animation (hearts, speech bubble, etc.).
        private static string EmotionFor(RecommendationTier tier) => tier switch
        {
            RecommendationTier.Excellent => "Perfect, that's exactly what I needed!",
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
