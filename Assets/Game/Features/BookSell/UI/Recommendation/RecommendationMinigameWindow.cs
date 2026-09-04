using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Localization;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using TMPro;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Book.Sell.UI
{
    /// <summary>
    /// Modal window for the active recommendation minigame (extracted from <see cref="SalesScreenView"/>).
    /// Shows the customer's request + the current shelf; clicking a book opens a detail panel; Recommend/Skip
    /// resolve the request via <see cref="ISalesDayController"/>; the resolution stamps success/fail over
    /// the selected book, keeps the customer's reaction text, and reveals a Finish button that
    /// closes the window. The day is paused by <see cref="SalesScreenView"/> while this window is open, so the
    /// shelf is static and rendered once on show.
    /// </summary>
    [Window("RecommendationMinigameWindow", WindowType.Popup, true)]
    public sealed class RecommendationMinigameWindow : WindowController<RecommendationMinigameWindowView>
    {
        private const int FallbackFinishButtonDelayMs = 3000;
        private const int FallbackEmotionTypeDurationMs = 1000;

        private ISalesDayController _controller;
        private IUiSpriteProvider _uiSprites;
        private readonly IBookConditionRequestEvaluator _conditionEvaluator = new BookConditionRequestEvaluator();
        private readonly List<BookCardView> _cards = new();
        private string _selectedBookId;
        private bool _subscribed;
        private bool _resolutionPending;
        private bool _resultShown;
        private int _fallbackFinishDelayVersion;

        // Resolved from the bootstrap scope (global singleton), same as DialogWindow injects IConfigsService.
        // Null-safe: BookCardView.Bind skips the icon load when the provider is unavailable.
        [Inject]
        public void InjectSprites(IUiSpriteProvider uiSprites) => _uiSprites = uiSprites;

        private static bool DebugToolsEnabled => Debug.isDebugBuild;

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
            //if (View.ResultPanel != null) View.ResultPanel.SetActive(false);
            if (View.Animator != null)
            {
                View.Animator.SetResultObjects(View.SuccessResultObject, View.FailResultObject);
                View.Animator.PrepareForRequest();
            }
            else
            {
                SetDetailState(hasBook: false);
                SetFinishButtonVisible(false, interactable: false);
            }

            _resolutionPending = false;
            _resultShown = false;
            RenderRequest(_controller.CurrentRequest);
            PopulateShelfCards();
            ClearSelection();

            // The window is keepInCache, so it is reused for every request. A previous request's
            // resolution left Skip/ClearFocus disabled (SetSelectionActionsInteractable(false)); re-enable
            // them on each show so the cancel/exit buttons never stay stuck off. Recommend stays off until
            // a book is selected (the method guards it on the current selection).
            SetSelectionActionsInteractable(true);

            View.Animator?.PlayRequestIntro(showShelfPanel: View.Animation is not RecommendationMinigameWindowAnimation);

            Subscribe();
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            View?.Animator?.KillAll();
            _fallbackFinishDelayVersion++;
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
                SetDebugState(card, shelfBook.Config);

                _cards.Add(card);
            }
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
                if (card != null) Object.Destroy(card.gameObject);
            _cards.Clear();
        }

        private void SetDebugState(BookCardView card, BookConfig book)
        {
            if (card == null) return;

            var request = _controller?.CurrentRequest?.ConditionRequest;
            var enabled = DebugToolsEnabled && request != null;
            card.SetDebugInfo(enabled, OnBookInfoClicked);

            bool? match = null;
            if (enabled && _conditionEvaluator != null && book != null)
                match = _conditionEvaluator.Evaluate(book, request).IsMatch;

            card.SetRequestMatch(match);
        }

        private void OnBookInfoClicked(string bookId, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(bookId) || anchor == null) return;

            var book = _controller?.Shelf?.Find(bookId)?.Config;
            if (book == null) return;

            ShowBookInfoWidgetAsync(book, anchor).Forget();
        }

        private async UniTaskVoid ShowBookInfoWidgetAsync(BookConfig book, RectTransform anchor)
        {
            if (book == null || anchor == null || UIManager == null || View == null) return;

            try
            {
                var data = new BookInfoWidgetData(
                    LocalizationLocator.GetOrKey(book.TitleKey),
                    JoinOrDash(book.Genres),
                    JoinOrDash(book.Qualities));

                await UIManager.ShowAsync<ContentWidgetController>(
                    new ContentWidgetArgs(data, anchor, this),
                    View.destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[RecommendationMinigameWindow] Failed to show book info widget for '{book.Id}': {e}");
            }
        }

        // ---------- selection + detail ----------

        private void OnBookCardClicked(string bookId)
        {
            if (_resolutionPending) return;

            _selectedBookId = bookId;
            foreach (var card in _cards) card.SetSelected(card.BookId == bookId);

            ShowDetail(_controller.Shelf.Find(bookId)?.Config);
            if (View.RecommendButton != null) View.RecommendButton.interactable = true;
        }

        private void ShowDetail(BookConfig book)
        {
            SetDetailState(book != null);
            if (book == null) return;

            Set(View.DetailTitle, LocalizationLocator.GetOrKey(book.TitleKey));
            Set(View.DetailAuthor, LocalizationLocator.GetOrKey(book.AuthorKey));
            Set(View.DetailDescription, LocalizationLocator.GetOrKey(book.DescriptionKey));
            Set(View.DetailPublishDate, $"{book.Published} y");
            Set(View.DetailPageCount, $"{book.Pages} p");

            View.Animator?.ShowBookDetail();
        }

        private void ClearSelection()
        {
            _selectedBookId = null;
            foreach (var card in _cards) card.SetSelected(false);

            // The detail area is not hidden here — it stays up and swaps back to the placeholder. The
            // instant reset also kills a running show-tween, so a half-faded panel cannot stick.
            View.Animator?.ShowBookDetailInstant();
            SetDetailState(hasBook: false);
            ClearDetailText();

            if (View.RecommendButton != null) View.RecommendButton.interactable = false;
        }

        // The detail area never leaves the screen during selection: exactly one of the two containers
        // is active. The animator's _bookDetailRoot is the area itself (it holds both containers), so
        // it is only taken away by HideSelection when the window moves to the result.
        private void SetDetailState(bool hasBook)
        {
            if (View.DetailSelectedRoot != null) View.DetailSelectedRoot.SetActive(hasBook);
            if (View.DetailEmptyRoot != null) View.DetailEmptyRoot.SetActive(!hasBook);
        }

        private void ClearDetailText()
        {
            Set(View.DetailTitle, string.Empty);
            Set(View.DetailAuthor, string.Empty);
            Set(View.DetailDescription, string.Empty);
            Set(View.DetailPublishDate, string.Empty);
            Set(View.DetailPageCount, string.Empty);
        }

        // ---------- actions ----------

        private void OnClearFocus()
        {
            if (_resolutionPending) return;
            ClearSelection();
        }

        private void OnRecommend()
        {
            if (_resolutionPending || _controller == null || string.IsNullOrEmpty(_selectedBookId)) return;

            _resolutionPending = true;
            SetSelectionActionsInteractable(false);
            _controller.RecommendBook(_selectedBookId);
        }

        private void OnSkip()
        {
            if (_resolutionPending || _controller == null) return;

            _resolutionPending = true;
            SetSelectionActionsInteractable(false);
            _controller.SkipCurrentRequest();

            if (!_resultShown)
                OnResolved(RecommendationResult.Skipped(_controller.CurrentRequest?.Id));
        }

        private void OnFinish() => CloseAsync().Forget();

        // ---------- resolution ----------

        private void OnResolved(RecommendationResult result)
        {
            if (_resultShown) return;

            _resultShown = true;
            _resolutionPending = true;
            SetSelectionActionsInteractable(false);

            var emotion = EmotionFor(result?.Tier ?? RecommendationTier.Skipped);
            if (View.Animator != null)
            {
                //if (View.ResultPanel != null) View.ResultPanel.SetActive(true);
                View.Animator.PlayResult(emotion, result?.Tier ?? RecommendationTier.Skipped, SelectedBookRect());
            }
            else
            {
                //if (View.ResultPanel != null) View.ResultPanel.SetActive(true);
                PlayFallbackResultAsync(emotion, ++_fallbackFinishDelayVersion).Forget();
            }
        }

        private async UniTaskVoid PlayFallbackResultAsync(string emotion, int version)
        {
            SetFinishButtonVisible(false, interactable: false);
            TypeFallbackEmotionAsync(emotion, version).Forget();
            await UniTask.Delay(FallbackFinishButtonDelayMs, ignoreTimeScale: true);
            if (version != _fallbackFinishDelayVersion || View == null) return;
            SetFinishButtonVisible(true, interactable: true);
        }

        private async UniTaskVoid TypeFallbackEmotionAsync(string emotion, int version)
        {
            var label = View?.EmotionLabel;
            if (label == null) return;

            label.text = emotion ?? string.Empty;
            label.maxVisibleCharacters = 0;
            label.ForceMeshUpdate();

            var characterCount = label.textInfo.characterCount;
            if (FallbackEmotionTypeDurationMs <= 0 || characterCount <= 0)
            {
                label.maxVisibleCharacters = int.MaxValue;
                return;
            }

            var duration = FallbackEmotionTypeDurationMs / 1000f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (version != _fallbackFinishDelayVersion || View == null) return;

                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                label.maxVisibleCharacters = Mathf.CeilToInt(characterCount * progress);
            }

            label.maxVisibleCharacters = int.MaxValue;
        }

        private RectTransform SelectedBookRect()
        {
            if (string.IsNullOrEmpty(_selectedBookId)) return null;

            foreach (var card in _cards)
            {
                if (card == null || card.BookId != _selectedBookId) continue;
                return card.GetComponent<RectTransform>();
            }

            return null;
        }

        private void SetSelectionActionsInteractable(bool interactable)
        {
            if (View.RecommendButton != null) View.RecommendButton.interactable = interactable && !string.IsNullOrEmpty(_selectedBookId);
            if (View.SkipButton != null) View.SkipButton.interactable = interactable;
            if (View.ClearFocusButton != null) View.ClearFocusButton.interactable = interactable;
        }

        private void SetFinishButtonVisible(bool visible, bool interactable)
        {
            if (View.FinishButton == null) return;
            View.FinishButton.gameObject.SetActive(visible);
            View.FinishButton.interactable = interactable;
        }

        // TODO: replace with proper reaction art/animation (hearts, speech bubble, etc.).
        private static string EmotionFor(RecommendationTier tier) => tier switch
        {
            RecommendationTier.Excellent => "Perfect, that's exactly what I needed!",
            RecommendationTier.Failed => "Hmm, that's not what I wanted...",
            RecommendationTier.Skipped => "Maybe next time.",
            _ => string.Empty
        };

        private static void Set(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value ?? string.Empty;
            label.maxVisibleCharacters = int.MaxValue;
        }

        private static string JoinOrDash(IReadOnlyList<string> values)
            => values == null || values.Count == 0 ? "-" : string.Join(", ", values);
    }
}
