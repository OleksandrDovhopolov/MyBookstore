using System.Collections.Generic;
using System.Threading;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Book.Sell.UI
{
    /// <summary>
    /// Debug screen for the Sales phase: header + current-request panel + shelf grid + two
    /// action buttons + feedback log + day-end panel. All logic lives in
    /// <see cref="ISalesSessionService"/>; the view only renders snapshots and emits input.
    ///
    /// Scene placement: GameplayScene -> Canvas -> a GameObject with this script + child UI
    /// elements. Registered via RegisterComponentInHierarchy in BookSellVContainerBindings.
    /// </summary>
    public sealed class SalesScreenView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _locationLabel;
        [SerializeField] private TMP_Text _goldLabel;
        [SerializeField] private TMP_Text _progressLabel;       // "Request 2 / 5"

        [Header("Active request")]
        [SerializeField] private GameObject _requestPanel;
        [SerializeField] private TMP_Text _requestText;

        [Header("Shelf (grid)")]
        [Tooltip("Container for book cards (typically a GridLayoutGroup or VerticalLayoutGroup).")]
        [SerializeField] private Transform _shelfContainer;
        [SerializeField] private BookCardView _bookCardPrefab;

        [Header("Actions")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _skipButton;

        [Header("Feedback log")]
        [SerializeField] private TMP_Text _feedbackLog;
        [SerializeField] [Min(1)] private int _maxLogLines = 8;

        [Header("Day end")]
        [SerializeField] private GameObject _dayEndPanel;
        [SerializeField] private TMP_Text _dayEndSummary;
        [SerializeField] private Button _restartButton;        // optional

        private ISalesSessionService _sales;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<BookCardView> _cards = new();
        private readonly Queue<string> _logLines = new();
        private string _selectedBookId;
        private bool _interactionAllowed;

        [Inject]
        public void Construct(ISalesSessionService sales)
        {
            _sales = sales;
        }

        private void Awake()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
            if (_restartButton != null) _restartButton.onClick.AddListener(OnRestartClicked);

            SetActionsInteractable(false);
            if (_dayEndPanel != null) _dayEndPanel.SetActive(false);
            if (_requestPanel != null) _requestPanel.SetActive(false);
            if (_feedbackLog != null) _feedbackLog.text = "";
        }

        private void Start()
        {
            if (_sales == null)
            {
                Debug.LogWarning("[SalesScreenView] ISalesSessionService was not injected — the screen is inactive.");
                return;
            }

            _sales.ActiveRequestStarted += OnActiveRequestStarted;
            _sales.RecommendationResolved += OnRecommendationResolved;
            _sales.PassiveSaleHappened += OnPassiveSaleHappened;
            _sales.DayCompleted += OnDayCompleted;

            StartDayFlowAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid StartDayFlowAsync(CancellationToken ct)
        {
            // ActiveRequestStarted is emitted synchronously inside StartDayAsync; cards do not
            // exist yet at that point, which is fine — the shelf snapshot is materialized below
            // right after the await.
            await _sales.StartDayAsync(day: 1, ct);
            PopulateShelfCards();
            RefreshHeader();
        }

        // ---------- service events ----------

        private void OnActiveRequestStarted(RequestConfig req)
        {
            if (_requestPanel != null) _requestPanel.SetActive(true);
            if (_requestText != null) _requestText.text = req.Text;

            _selectedBookId = null;
            foreach (var c in _cards) c.SetSelected(false);

            _interactionAllowed = true;
            SetActionsInteractable(true);
            RefreshHeader();
        }

        private void OnRecommendationResolved(RecommendationResult result)
        {
            // A book leaves the shelf only on Normal/Excellent. Failed and Skipped do not sell.
            if (!string.IsNullOrEmpty(result.BookId) &&
                (result.Tier == RecommendationTier.Excellent || result.Tier == RecommendationTier.Normal))
            {
                FindCard(result.BookId)?.SetSoldOut(true);
            }

            AppendLog(BuildResultLine(result));

            // Input stays disabled between a resolved active request and the next one starting —
            // it is re-enabled by ActiveRequestStarted.
            _interactionAllowed = false;
            SetActionsInteractable(false);
            RefreshHeader();
        }

        private void OnPassiveSaleHappened(PassiveSaleEvent evt)
        {
            FindCard(evt.BookId)?.SetSoldOut(true);
            AppendLog($"<i>passive sale: {evt.BookId}  +{evt.GoldEarned}</i>");
            RefreshHeader();
        }

        private void OnDayCompleted(SalesDayResult result)
        {
            _interactionAllowed = false;
            SetActionsInteractable(false);

            if (_requestPanel != null) _requestPanel.SetActive(false);

            if (_dayEndPanel != null) _dayEndPanel.SetActive(true);
            if (_dayEndSummary != null)
            {
                _dayEndSummary.text =
                    $"<b>Day {result.Day} completed</b>\n\n" +
                    $"Sales: {result.SalesCount}  |  Revenue: {result.GoldEarned}\n" +
                    $"Excellent: {result.ExcellentCount}   Normal: {result.NormalCount}   " +
                    $"Failed: {result.FailedCount}   Skipped: {result.SkippedCount}";
            }

            Debug.Log($"[SalesScreenView] DayCompleted: day={result.Day}, " +
                      $"sales={result.SalesCount}, gold={result.GoldEarned}, " +
                      $"excellent={result.ExcellentCount}, normal={result.NormalCount}, " +
                      $"failed={result.FailedCount}, skipped={result.SkippedCount}");
        }

        // ---------- user input ----------

        private void OnBookCardClicked(string bookId)
        {
            if (!_interactionAllowed) return;
            _selectedBookId = bookId;
            foreach (var c in _cards) c.SetSelected(c.BookId == bookId);
            SetActionsInteractable(true);
        }

        private void OnConfirmClicked()
        {
            if (!_interactionAllowed || string.IsNullOrEmpty(_selectedBookId)) return;
            _sales.RecommendBook(_selectedBookId);
        }

        private void OnSkipClicked()
        {
            if (!_interactionAllowed) return;
            _sales.SkipCurrentRequest();
        }

        private void OnRestartClicked()
        {
            // Restart the day without reloading the scene.
            ClearCards();
            _logLines.Clear();
            if (_feedbackLog != null) _feedbackLog.text = "";
            if (_dayEndPanel != null) _dayEndPanel.SetActive(false);
            _selectedBookId = null;
            StartDayFlowAsync(_cts.Token).Forget();
        }

        // ---------- shelf ----------

        private void PopulateShelfCards()
        {
            ClearCards();
            if (_bookCardPrefab == null || _shelfContainer == null) return;

            foreach (var shelfBook in _sales.State.Shelf)
            {
                var card = Instantiate(_bookCardPrefab, _shelfContainer);
                card.Bind(shelfBook.Config, OnBookCardClicked);
                card.SetSoldOut(shelfBook.State == ShelfBookState.SoldOut);
                _cards.Add(card);
            }
        }

        private void ClearCards()
        {
            foreach (var c in _cards)
                if (c != null) Destroy(c.gameObject);
            _cards.Clear();
        }

        private BookCardView FindCard(string bookId)
        {
            foreach (var c in _cards)
                if (c.BookId == bookId) return c;
            return null;
        }

        // ---------- log / header ----------

        private void AppendLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > _maxLogLines) _logLines.Dequeue();
            if (_feedbackLog != null) _feedbackLog.text = string.Join("\n", _logLines);
        }

        private string BuildResultLine(RecommendationResult result)
        {
            if (result.Tier == RecommendationTier.Skipped) return "<i>— nothing offered</i>";

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
            var state = _sales.State;
            var result = _sales.AccumulatedResult;

            if (_dayLabel != null) _dayLabel.text = $"Day {state.Day}";
            if (_locationLabel != null) _locationLabel.text = state.LocationId ?? "—";
            if (_goldLabel != null) _goldLabel.text = result.GoldEarned.ToString();
            if (_progressLabel != null)
            {
                var total = state.ActiveQueue.Count;
                var idx = state.CurrentRequestIndex < 0 ? total : state.CurrentRequestIndex + 1;
                _progressLabel.text = total > 0 ? $"Request {idx} / {total}" : "—";
            }
        }

        private void SetActionsInteractable(bool active)
        {
            if (_confirmButton != null)
                _confirmButton.interactable = active && !string.IsNullOrEmpty(_selectedBookId);
            if (_skipButton != null)
                _skipButton.interactable = active;
        }

        // ---------- lifecycle ----------

        private void OnDestroy()
        {
            if (_sales != null)
            {
                _sales.ActiveRequestStarted -= OnActiveRequestStarted;
                _sales.RecommendationResolved -= OnRecommendationResolved;
                _sales.PassiveSaleHappened -= OnPassiveSaleHappened;
                _sales.DayCompleted -= OnDayCompleted;
            }

            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestartClicked);

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
