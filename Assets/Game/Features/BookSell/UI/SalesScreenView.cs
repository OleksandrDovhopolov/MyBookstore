using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
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
    /// renders the header + feedback log, and drives the end-of-day flow. The active recommendation
    /// minigame (request + shelf + Confirm/Skip + book detail + result) now lives in
    /// <see cref="RecommendationMinigameWindow"/>, opened by <see cref="RecommendationMinigamePresenter"/>;
    /// this screen only pauses the day while that window is open (via
    /// <see cref="IRecommendationMinigamePresenter.IsWindowOpen"/>). All logic is in
    /// <see cref="ISalesDayController"/>; the view only renders snapshots and forwards player input.
    /// </summary>
    public sealed class SalesScreenView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _locationLabel;
        [SerializeField] private TMP_Text _goldLabel;
        [SerializeField] private TMP_Text _progressLabel;       // "Served: N"

        [Header("End of day")]
        [SerializeField] private Button _closeShopButton;   // "Свернуть лавку" — shown when day is ReadyToClose

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
        private readonly Queue<FeedbackLogEntryView> _entries = new();
        private bool _dayRunning;

        private ICurrentDayProvider _dayProvider;
        private IUIManager _uiManager;
        private IRecommendationMinigamePresenter _minigamePresenter;
        private IPublisher<GameplaySceneButtonsInteractableChanged> _gameplayButtonsPublisher;
        private IPublisher<GameplayGenreBookCountsChanged> _genreBookCountsPublisher;
        private IDisposable _genreBookCountsRequestSubscription;

        [Inject]
        public void Construct(
            ISalesDayController controller,
            ICurrentDayProvider dayProvider = null,
            IUIManager uiManager = null,
            IRecommendationMinigamePresenter minigamePresenter = null,
            IPublisher<GameplaySceneButtonsInteractableChanged> gameplayButtonsPublisher = null,
            IPublisher<GameplayGenreBookCountsChanged> genreBookCountsPublisher = null,
            ISubscriber<GameplayGenreBookCountsRequested> genreBookCountsRequestSubscriber = null)
        {
            _controller = controller;
            _dayProvider = dayProvider;
            _uiManager = uiManager;
            _minigamePresenter = minigamePresenter;
            _gameplayButtonsPublisher = gameplayButtonsPublisher;
            _genreBookCountsPublisher = genreBookCountsPublisher;
            _genreBookCountsRequestSubscription = genreBookCountsRequestSubscriber?.Subscribe(_ => PublishGenreBookCounts());
        }

        private void Awake()
        {
            if (_restartButton != null) _restartButton.onClick.AddListener(OnRestartClicked);
            if (_closeShopButton != null)
            {
                _closeShopButton.onClick.AddListener(OnCloseShopClicked);
                _closeShopButton.gameObject.SetActive(false);
            }

            if (_dayEndPanel != null) _dayEndPanel.SetActive(false);
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

            _controller.RecommendationResolved += OnRecommendationResolved;
            _controller.PassiveSaleHappened += OnPassiveSaleHappened;
            _controller.DayReadyToClose += OnDayReadyToClose;
            _controller.DayCompleted += OnDayCompleted;
            _controller.BookReserved += OnBookReserved;
            _controller.ShelfChanged += OnShelfChanged;

            StartDayFlowAsync(_cts.Token).Forget();
        }

        private void Update()
        {
            // The day is paused while the recommendation minigame window is open (owned by the presenter).
            if (!_dayRunning || _controller == null || (_minigamePresenter?.IsWindowOpen ?? false)) return;
            _controller.Tick(Time.deltaTime);
            RefreshHeader();
        }

        private async UniTaskVoid StartDayFlowAsync(CancellationToken ct)
        {
            // Day comes from DayCycle.DayProgressService via the ICurrentDayProvider adapter.
            // When the adapter is not registered (e.g. early prototype scenes), fall back to day 1.
            var day = _dayProvider?.CurrentDay ?? 1;
            await _controller.StartDayAsync(day, ct);
            RefreshHeader();
            PublishGenreBookCounts();
            _dayRunning = !_controller.IsDayCompleted;
            SetGameplaySceneButtonsInteractable(!_dayRunning);
        }

        // ---------- controller events ----------

        private void OnRecommendationResolved(RecommendationResult result)
        {
            // The minigame window owns the in-window result UI; here we only append to the feedback log.
            AppendActiveEntry(result);
        }

        private void OnBookReserved(Domain.Customer customer, string bookId)
        {
            AppendEntry(
                FeedbackLogEntryView.EntryKind.BookReserved,
                $"<i>{customer.Id} reserved {bookId}</i>");
        }

        private void OnShelfChanged()
        {
            PublishGenreBookCounts();
        }

        private void PublishGenreBookCounts()
        {
            _genreBookCountsPublisher?.Publish(new GameplayGenreBookCountsChanged(BuildGenreBookCounts()));
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

        private void OnDayReadyToClose()
        {
            // Day's work is done: stop pumping the sim and let the player close the shop manually.
            _dayRunning = false;
            RefreshHeader();

            if (_closeShopButton != null)
            {
                _closeShopButton.gameObject.SetActive(true);
                _closeShopButton.interactable = true;
            }
        }

        private void OnCloseShopClicked()
        {
            if (_controller == null) return;
            if (_closeShopButton != null) _closeShopButton.interactable = false;

            // Presentation hook: a short shop-closing animation can play here before concluding.
            // MVP concludes immediately; OnDayCompleted then opens the ResultsWindow.
            _controller.ConcludeDay();
        }

        private void OnDayCompleted(SalesDayResult result)
        {
            _dayRunning = false;
            SetGameplaySceneButtonsInteractable(true);
            if (_closeShopButton != null) _closeShopButton.gameObject.SetActive(false);

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

        private void OnRestartClicked()
        {
            ClearEntries();
            if (_dayEndPanel != null) _dayEndPanel.SetActive(false);
            if (_closeShopButton != null) _closeShopButton.gameObject.SetActive(false);
            SetGameplaySceneButtonsInteractable(false);
            StartDayFlowAsync(_cts.Token).Forget();
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

        private void SetGameplaySceneButtonsInteractable(bool interactable)
        {
            _gameplayButtonsPublisher?.Publish(new GameplaySceneButtonsInteractableChanged(interactable));
        }

        private Dictionary<string, int> BuildGenreBookCounts()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var books = _controller?.Shelf?.Books;
            if (books == null) return counts;

            for (var i = 0; i < books.Count; i++)
            {
                var book = books[i];
                if (book == null || book.State != ShelfBookState.Available) continue;

                var genre = book.Config?.Genre;
                if (string.IsNullOrEmpty(genre)) continue;

                counts.TryGetValue(genre, out var count);
                counts[genre] = count + 1;
            }

            return counts;
        }

        private void OnDestroy()
        {
            SetGameplaySceneButtonsInteractable(true);
            _genreBookCountsRequestSubscription?.Dispose();
            _genreBookCountsRequestSubscription = null;

            if (_controller != null)
            {
                _controller.RecommendationResolved -= OnRecommendationResolved;
                _controller.PassiveSaleHappened -= OnPassiveSaleHappened;
                _controller.DayReadyToClose -= OnDayReadyToClose;
                _controller.DayCompleted -= OnDayCompleted;
                _controller.BookReserved -= OnBookReserved;
                _controller.ShelfChanged -= OnShelfChanged;
            }

            if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestartClicked);
            if (_closeShopButton != null) _closeShopButton.onClick.RemoveListener(OnCloseShopClicked);

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
