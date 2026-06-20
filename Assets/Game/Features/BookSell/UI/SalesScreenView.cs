using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Results.UI;
using Game.UI;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Book.Sell.UI
{
    // This class is used in SalesCheatModule FindAnyObjectByType. when refactor remove need update SalesCheatModule ( using DI ? ) //
    
    /// <summary>
    /// Debug screen for the real-time Sales phase (ADR-0003). Pumps the day controller from Update,
    /// renders the shelf live (reserved/sold books drop out), and exposes the active minigame via the
    /// request panel + Confirm/Skip. All logic is in <see cref="ISalesDayController"/>; the view only
    /// renders snapshots and forwards player input. Animated movement/popups are out of scope here.
    /// </summary>
    public sealed class SalesScreenView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _locationLabel;
        [SerializeField] private TMP_Text _goldLabel;
        [SerializeField] private TMP_Text _progressLabel;       // "Served: N"

        [Header("Active request (minigame)")]
        [SerializeField] private GameObject _requestPanel;
        [SerializeField] private TMP_Text _requestText;
        [SerializeField] private TMP_Text _difficultyLabel;     // "Difficulty: 3/5" — empty when none/Unknown

        [Header("Shelf (grid)")]
        [SerializeField] private Transform _shelfContainer;
        [SerializeField] private BookCardView _bookCardPrefab;

        [Header("Actions")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _skipButton;

        [Header("Feedback log (vertical list of prefab entries)")]
        [SerializeField] private Transform _feedbackLogContainer;
        [SerializeField] private FeedbackLogEntryView _feedbackLogEntryPrefab;
        [SerializeField] [Min(1)] private int _maxLogLines = 12;

        [Header("Legacy day-end fallback (used only when Results Screen Root is empty)")]
        [SerializeField] private GameObject _dayEndPanel;
        [SerializeField] private TMP_Text _dayEndSummary;
        [SerializeField] private Button _restartButton;        // optional

        private ISalesDayController _controller;
        public ISalesDayController Controller => _controller;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<BookCardView> _cards = new();
        private readonly Queue<FeedbackLogEntryView> _entries = new();
        private string _selectedBookId;
        private bool _minigameOpen;
        private bool _dayRunning;

        private ICurrentDayProvider _dayProvider;
        private IUIManager _uiManager;
        private IPublisher<GameplaySceneButtonsInteractableChanged> _gameplayButtonsPublisher;

        [Inject]
        public void Construct(
            ISalesDayController controller,
            ICurrentDayProvider dayProvider = null,
            IUIManager uiManager = null,
            IPublisher<GameplaySceneButtonsInteractableChanged> gameplayButtonsPublisher = null)
        {
            _controller = controller;
            _dayProvider = dayProvider;
            _uiManager = uiManager;
            _gameplayButtonsPublisher = gameplayButtonsPublisher;
        }

        private void Awake()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
            if (_restartButton != null) _restartButton.onClick.AddListener(OnRestartClicked);

            if (_dayEndPanel != null) _dayEndPanel.SetActive(false);
            if (_requestPanel != null) _requestPanel.SetActive(false);
            if (_difficultyLabel != null) _difficultyLabel.text = "";
            SetActionsInteractable(false);
        }

        private void Start()
        {
            if (_controller == null)
            {
                Debug.LogWarning("[SalesScreenView] ISalesDayController was not injected — the screen is inactive.");
                return;
            }

            // On restart, the player may have already completed today's sales but not yet pressed
            // Next Day. In that case, skip Sales entirely and hand straight to Results.
            if (_dayProvider != null && _dayProvider.IsCurrentDayCompleted)
            {
                ShowResultsWindowAsync().Forget();
                return;
            }

            _controller.ActiveRequestStarted += OnActiveRequestStarted;
            _controller.RecommendationResolved += OnRecommendationResolved;
            _controller.PassiveSaleHappened += OnPassiveSaleHappened;
            _controller.DayCompleted += OnDayCompleted;
            _controller.BookReserved += OnBookReserved;

            StartDayFlowAsync(_cts.Token).Forget();
        }

        private void Update()
        {
            if (!_dayRunning || _controller == null) return;
            _controller.Tick(Time.deltaTime);
            RefreshShelfAvailability();
            RefreshHeader();
        }

        private async UniTaskVoid StartDayFlowAsync(CancellationToken ct)
        {
            // Day comes from DayCycle.DayProgressService via the ICurrentDayProvider adapter.
            // When the adapter is not registered (e.g. early prototype scenes), fall back to day 1.
            var day = _dayProvider?.CurrentDay ?? 1;
            await _controller.StartDayAsync(day, ct);
            PopulateShelfCards();
            RefreshHeader();
            _dayRunning = !_controller.IsDayCompleted;
            SetGameplaySceneButtonsInteractable(!_dayRunning);
        }

        // ---------- controller events ----------

        private void OnActiveRequestStarted(RequestConfig req)
        {
            _minigameOpen = true;
            if (_requestPanel != null) _requestPanel.SetActive(true);
            if (_requestText != null) _requestText.text = req.Text;
            if (_difficultyLabel != null)
                _difficultyLabel.text = req.Difficulty == RequestDifficulty.Unknown
                    ? ""
                    : $"Difficulty: {(int)req.Difficulty}/5";

            _selectedBookId = null;
            foreach (var c in _cards) c.SetSelected(false);
            SetActionsInteractable(true);
        }

        private void OnRecommendationResolved(RecommendationResult result)
        {
            AppendActiveEntry(result);

            _minigameOpen = false;
            _selectedBookId = null;
            if (_requestPanel != null) _requestPanel.SetActive(false);
            if (_difficultyLabel != null) _difficultyLabel.text = "";
            SetActionsInteractable(false);
        }

        private void OnBookReserved(Domain.Customer customer, string bookId)
        {
            AppendEntry(
                FeedbackLogEntryView.EntryKind.BookReserved,
                $"<i>{customer.Id} reserved {bookId}</i>");
        }

        private void OnPassiveSaleHappened(PassiveSaleEvent evt)
        {
            var demand = new List<string>(evt.MatchedGenres.Count + evt.MatchedTags.Count);
            demand.AddRange(evt.MatchedGenres);
            demand.AddRange(evt.MatchedTags);
            var demandSuffix = demand.Count > 0 ? $"  demand: {string.Join(", ", demand)}" : "";

            AppendEntry(
                FeedbackLogEntryView.EntryKind.PassiveSale,
                $"<i>passive sale: {evt.BookId}  +{evt.GoldEarned}{demandSuffix}</i>");
        }

        private void OnDayCompleted(SalesDayResult result)
        {
            _dayRunning = false;
            _minigameOpen = false;
            SetActionsInteractable(false);
            SetGameplaySceneButtonsInteractable(true);
            if (_requestPanel != null) _requestPanel.SetActive(false);

            Debug.Log($"[SalesScreenView] DayCompleted: day={result.Day}, customers={result.CustomersServed}, " +
                      $"sales={result.SalesCount}, gold={result.GoldEarned}, " +
                      $"excellent={result.ExcellentCount}, normal={result.NormalCount}, " +
                      $"failed={result.FailedCount}, skipped={result.SkippedCount}");

            ShowResultsWindowAsync().Forget();
        }

        private async UniTaskVoid ShowResultsWindowAsync()
        {
            if (_uiManager == null)
            {
                Debug.LogWarning("[SalesScreenView] IUIManager was not injected - cannot open ResultsWindow.");
                ShowLegacyDayEndPanel();
                return;
            }

            var window = await _uiManager.ShowAsync<ResultsWindow>(ct: _cts.Token);
            if (window != null)
                gameObject.SetActive(false);
        }

        private void ShowLegacyDayEndPanel()
        {
            var result = _controller.AccumulatedResult;
            if (_dayEndPanel != null) _dayEndPanel.SetActive(true);
            if (_dayEndSummary != null)
            {
                _dayEndSummary.text =
                    $"<b>Day {result.Day} completed</b>\n\n" +
                    $"Sales: {result.SalesCount}  |  Revenue: {result.GoldEarned}\n" +
                    $"Customers: {result.CustomersServed}\n" +
                    $"Excellent: {result.ExcellentCount}   Normal: {result.NormalCount}   " +
                    $"Failed: {result.FailedCount}   Skipped: {result.SkippedCount}";
            }
        }

        // ---------- user input ----------

        private void OnBookCardClicked(string bookId)
        {
            if (!_minigameOpen) return;   // selection only matters during an active minigame
            _selectedBookId = bookId;
            foreach (var c in _cards) c.SetSelected(c.BookId == bookId);
            SetActionsInteractable(true);
        }

        private void OnConfirmClicked()
        {
            if (!_minigameOpen || string.IsNullOrEmpty(_selectedBookId)) return;
            _controller.RecommendBook(_selectedBookId);
        }

        private void OnSkipClicked()
        {
            if (!_minigameOpen) return;
            _controller.SkipCurrentRequest();
        }

        private void OnRestartClicked()
        {
            ClearEntries();
            if (_dayEndPanel != null) _dayEndPanel.SetActive(false);
            _selectedBookId = null;
            _minigameOpen = false;
            SetGameplaySceneButtonsInteractable(false);
            StartDayFlowAsync(_cts.Token).Forget();
        }

        // ---------- shelf ----------

        private void PopulateShelfCards()
        {
            ClearCards();
            if (_bookCardPrefab == null || _shelfContainer == null) return;

            foreach (var shelfBook in _controller.Shelf.Books)
            {
                var card = Instantiate(_bookCardPrefab, _shelfContainer);
                card.Bind(shelfBook.Config, OnBookCardClicked);
                _cards.Add(card);
            }
            RefreshShelfAvailability();
        }

        private void RefreshShelfAvailability()
        {
            if (_cards.Count == 0) return;

            var shelf = _controller.Shelf;
            var selectionStillValid = false;

            foreach (var card in _cards)
            {
                var book = shelf.Find(card.BookId);
                var available = book != null
                                && book.State == ShelfBookState.Available
                                && !shelf.IsReserved(card.BookId);
                card.SetSoldOut(!available);

                if (available && card.BookId == _selectedBookId)
                    selectionStillValid = true;
            }

            // Selected book got sold/reserved out from under the player → drop the selection.
            if (!string.IsNullOrEmpty(_selectedBookId) && !selectionStillValid)
            {
                _selectedBookId = null;
                foreach (var c in _cards) c.SetSelected(false);
                if (_minigameOpen) SetActionsInteractable(true);
            }
        }

        private void ClearCards()
        {
            foreach (var c in _cards)
                if (c != null) Destroy(c.gameObject);
            _cards.Clear();
        }

        // ---------- feedback log ----------

        private void AppendActiveEntry(RecommendationResult result)
        {
            FeedbackLogEntryView.EntryKind kind;
            string text;

            switch (result.Tier)
            {
                case RecommendationTier.Excellent:
                    kind = FeedbackLogEntryView.EntryKind.ActiveExcellent;
                    text = BuildResultLine(result);
                    break;
                case RecommendationTier.Normal:
                    kind = FeedbackLogEntryView.EntryKind.ActiveNormal;
                    text = BuildResultLine(result);
                    break;
                case RecommendationTier.Failed:
                    kind = FeedbackLogEntryView.EntryKind.ActiveFailed;
                    text = BuildResultLine(result);
                    break;
                case RecommendationTier.Skipped:
                default:
                    kind = FeedbackLogEntryView.EntryKind.ActiveSkipped;
                    text = "<i>— nothing offered</i>";
                    break;
            }

            AppendEntry(kind, text);
        }

        private void AppendEntry(FeedbackLogEntryView.EntryKind kind, string text)
        {
            if (_feedbackLogEntryPrefab == null || _feedbackLogContainer == null) return;

            var entry = Instantiate(_feedbackLogEntryPrefab, _feedbackLogContainer);
            entry.Bind(kind, text);
            _entries.Enqueue(entry);

            while (_entries.Count > _maxLogLines)
            {
                var oldest = _entries.Dequeue();
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }

        private void ClearEntries()
        {
            while (_entries.Count > 0)
            {
                var e = _entries.Dequeue();
                if (e != null) Destroy(e.gameObject);
            }
        }

        private string BuildResultLine(RecommendationResult result)
        {
            var tierLabel = result.Tier switch
            {
                RecommendationTier.Excellent => "<b>Excellent</b>",
                RecommendationTier.Normal => "<b>Normal</b>",
                RecommendationTier.Failed => "<b>Failed</b>",
                _ => result.Tier.ToString()
            };

            var matched = new List<string>(5);
            if (result.Reason.MatchedGenres.Count > 0) matched.Add($"genre({string.Join(",", result.Reason.MatchedGenres)})");
            if (result.Reason.MatchedTags.Count > 0) matched.Add($"tags({string.Join(",", result.Reason.MatchedTags)})");
            if (result.Reason.MatchedMood.Count > 0) matched.Add($"mood({string.Join(",", result.Reason.MatchedMood)})");
            if (result.Reason.PriceFits) matched.Add("price");
            if (result.Reason.LocationBonus) matched.Add("location");

            var why = matched.Count > 0 ? $"  matched: {string.Join(", ", matched)}" : "";
            var gold = result.GoldEarned > 0 ? $"  +{result.GoldEarned}" : "";
            return $"{tierLabel}: {result.BookId}{why}{gold}";
        }

        private void RefreshHeader()
        {
            var result = _controller.AccumulatedResult;
            if (_dayLabel != null) _dayLabel.text = $"Day {_controller.Day}";
            if (_locationLabel != null) _locationLabel.text = _controller.LocationId ?? "—";
            if (_goldLabel != null) _goldLabel.text = result.GoldEarned.ToString();
            if (_progressLabel != null) _progressLabel.text = $"Served: {result.CustomersServed}";
        }

        private void SetActionsInteractable(bool active)
        {
            if (_confirmButton != null)
                _confirmButton.interactable = active && !string.IsNullOrEmpty(_selectedBookId);
            if (_skipButton != null)
                _skipButton.interactable = active;
        }

        private void SetGameplaySceneButtonsInteractable(bool interactable)
        {
            _gameplayButtonsPublisher?.Publish(new GameplaySceneButtonsInteractableChanged(interactable));
        }

        private void OnDestroy()
        {
            SetGameplaySceneButtonsInteractable(true);

            if (_controller != null)
            {
                _controller.ActiveRequestStarted -= OnActiveRequestStarted;
                _controller.RecommendationResolved -= OnRecommendationResolved;
                _controller.PassiveSaleHappened -= OnPassiveSaleHappened;
                _controller.DayCompleted -= OnDayCompleted;
                _controller.BookReserved -= OnBookReserved;
            }

            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestartClicked);

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
