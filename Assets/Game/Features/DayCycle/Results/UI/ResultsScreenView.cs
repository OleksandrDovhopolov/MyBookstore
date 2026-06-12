using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Results.Domain;
using Game.DayCycle.Results.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.DayCycle.Results.UI
{
    /// <summary>
    /// Debug results screen: header + numbers + best-match card + review line + reward line + Next Day.
    /// Numbers come straight from the persisted SalesDayResult through IResultsSessionService; the
    /// view never recomputes anything. GameObject is inactive by default; SalesScreenView activates
    /// it on day completion, and on restart the Sales view routes here when the day is already done.
    /// </summary>
    public sealed class ResultsScreenView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _salesLabel;
        [SerializeField] private TMP_Text _goldLabel;

        [Header("Tier counts")]
        [SerializeField] private TMP_Text _excLabel;
        [SerializeField] private TMP_Text _normLabel;
        [SerializeField] private TMP_Text _failLabel;
        [SerializeField] private TMP_Text _skipLabel;

        [Header("Best match")]
        [SerializeField] private GameObject _bestMatchPanel;
        [SerializeField] private TMP_Text _bestBookLabel;
        [SerializeField] private TMP_Text _bestRequestLabel;
        [SerializeField] private TMP_Text _bestTierLabel;
        [SerializeField] private TMP_Text _bestReasonLabel;

        [Header("Review")]
        [SerializeField] private TMP_Text _reviewLabel;

        [Header("Reward line")]
        [SerializeField] private TMP_Text _goldDeltaLabel;
        [SerializeField] private TMP_Text _repDeltaLabel;
        [SerializeField] private TMP_Text _alreadyAppliedHint;

        [Header("Actions")]
        [SerializeField] private Button _nextDayButton;

        [Header("Error")]
        [SerializeField] private GameObject _errorPanel;

        private IResultsSessionService _service;
        private readonly CancellationTokenSource _cts = new();

        [Inject]
        public void Construct(IResultsSessionService service)
        {
            _service = service;
        }

        private void Awake()
        {
            if (_nextDayButton != null)
            {
                _nextDayButton.onClick.AddListener(OnNextDayClicked);
                _nextDayButton.interactable = false;
            }
            if (_errorPanel != null) _errorPanel.SetActive(false);
            if (_alreadyAppliedHint != null) _alreadyAppliedHint.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_service == null)
            {
                Debug.LogWarning("[ResultsScreenView] IResultsSessionService not injected — screen inactive.");
                return;
            }

            _service.SummaryReady += OnSummaryReady;
            _service.NoResultAvailable += OnNoResultAvailable;
            _service.LoadAndApplyAsync(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            if (_service != null)
            {
                _service.SummaryReady -= OnSummaryReady;
                _service.NoResultAvailable -= OnNoResultAvailable;
            }
        }

        // ----- service events -----

        private void OnSummaryReady(ResultsSummary s)
        {
            Set(_dayLabel, $"Day {s.Day} completed");
            Set(_salesLabel, $"Sales: {s.SalesCount}");
            Set(_goldLabel, $"Revenue: {s.GoldEarned}");
            Set(_excLabel, $"Excellent: {s.ExcellentCount}");
            Set(_normLabel, $"Normal: {s.NormalCount}");
            Set(_failLabel, $"Failed: {s.FailedCount}");
            Set(_skipLabel, $"Skipped: {s.SkippedCount}");
            Set(_reviewLabel, s.ReviewText);
            Set(_goldDeltaLabel, $"+{s.GoldDelta} gold");
            Set(_repDeltaLabel,
                s.ReputationDelta >= 0 ? $"+{s.ReputationDelta} reputation" : $"{s.ReputationDelta} reputation");

            if (_alreadyAppliedHint != null)
                _alreadyAppliedHint.gameObject.SetActive(s.AlreadyApplied);

            RenderBestMatch(s.BestMatch);

            if (_nextDayButton != null) _nextDayButton.interactable = true;
        }

        private void OnNoResultAvailable()
        {
            if (_errorPanel != null) _errorPanel.SetActive(true);
            if (_nextDayButton != null) _nextDayButton.interactable = false;
            Debug.LogError("[ResultsScreenView] no SalesDayResult — Results cannot proceed.");
        }

        private void RenderBestMatch(BestMatchCard best)
        {
            if (_bestMatchPanel != null) _bestMatchPanel.SetActive(best != null);
            if (best == null) return;

            Set(_bestBookLabel, best.BookId);
            Set(_bestRequestLabel, best.RequestId);
            Set(_bestTierLabel, $"{best.Tier} (score {best.Score})");

            if (_bestReasonLabel != null)
            {
                var parts = new List<string>(5);
                if (best.Reason != null)
                {
                    if (best.Reason.MatchedGenres.Count > 0) parts.Add($"genre({string.Join(",", best.Reason.MatchedGenres)})");
                    if (best.Reason.MatchedTags.Count > 0) parts.Add($"tags({string.Join(",", best.Reason.MatchedTags)})");
                    if (best.Reason.MatchedMood.Count > 0) parts.Add($"mood({string.Join(",", best.Reason.MatchedMood)})");
                    if (best.Reason.PriceFits) parts.Add("price");
                    if (best.Reason.LocationBonus) parts.Add("location");
                }
                _bestReasonLabel.text = parts.Count > 0 ? $"matched: {string.Join(", ", parts)}" : "";
            }
        }

        // ----- user input -----

        private void OnNextDayClicked()
        {
            if (_nextDayButton != null) _nextDayButton.interactable = false;
            _service.AdvanceToNextDayAsync(_cts.Token).Forget();
        }

        // ----- lifecycle -----

        private void OnDestroy()
        {
            if (_nextDayButton != null) _nextDayButton.onClick.RemoveListener(OnNextDayClicked);
            _cts.Cancel();
            _cts.Dispose();
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
