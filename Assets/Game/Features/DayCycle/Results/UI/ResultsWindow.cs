using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Results.Domain;
using Game.DayCycle.Results.Services;
using Game.Newspaper.UI;
using Game.UI;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.DayCycle.Results.UI
{
    [Window("ResultsWindow", WindowType.Page)]
    public sealed class ResultsWindow : WindowController<ResultsWindowView>
    {
        private IResultsSessionService _service;
        private CancellationTokenSource _cts;
        private bool _subscribed;

        [Inject]
        public void InjectServices(IResultsSessionService service)
        {
            _service = service;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();

            if (View.NextDayButton != null)
            {
                View.NextDayButton.onClick.AddListener(OnNextDayClicked);
                View.NextDayButton.interactable = false;
            }

            if (View.OpenNewspaperButton != null)
                View.OpenNewspaperButton.onClick.AddListener(OnOpenNewspaperClicked);

            SetActive(View.ErrorPanel, false);
            if (View.AlreadyAppliedHint != null)
                View.AlreadyAppliedHint.gameObject.SetActive(false);
        }

        protected override void OnShowStart()
        {
            if (_service == null)
            {
                Debug.LogWarning("[ResultsWindow] IResultsSessionService not injected.");
                return;
            }

            Subscribe();
            SetActive(View.ErrorPanel, false);
            if (View.NextDayButton != null) View.NextDayButton.interactable = false;
            _service.LoadAndApplyAsync(_cts.Token).Forget();
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            Unsubscribe();
        }

        protected override void OnDispose()
        {
            Unsubscribe();

            if (View != null)
            {
                if (View.NextDayButton != null)
                    View.NextDayButton.onClick.RemoveListener(OnNextDayClicked);
                if (View.OpenNewspaperButton != null)
                    View.OpenNewspaperButton.onClick.RemoveListener(OnOpenNewspaperClicked);
            }
            
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void Subscribe()
        {
            if (_subscribed || _service == null) return;
            _service.SummaryReady += OnSummaryReady;
            _service.NoResultAvailable += OnNoResultAvailable;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _service == null) return;
            _service.SummaryReady -= OnSummaryReady;
            _service.NoResultAvailable -= OnNoResultAvailable;
            _subscribed = false;
        }

        private void OnSummaryReady(ResultsSummary summary)
        {
            Set(View.DayLabel, $"Day {summary.Day} completed");
            Set(View.SalesLabel, $"Sales: {summary.SalesCount}");
            Set(View.GoldLabel, $"Revenue: {summary.GoldEarned}");
            Set(View.ExcellentLabel, $"Excellent: {summary.ExcellentCount}");
            Set(View.NormalLabel, $"Normal: {summary.NormalCount}");
            Set(View.FailedLabel, $"Failed: {summary.FailedCount}");
            Set(View.SkippedLabel, $"Skipped: {summary.SkippedCount}");
            Set(View.ReviewLabel, summary.ReviewText);
            Set(View.GoldDeltaLabel, $"+{summary.GoldDelta} gold");
            Set(View.ReputationDeltaLabel,
                summary.ReputationDelta >= 0
                    ? $"+{summary.ReputationDelta} reputation"
                    : $"{summary.ReputationDelta} reputation");

            if (View.AlreadyAppliedHint != null)
                View.AlreadyAppliedHint.gameObject.SetActive(summary.AlreadyApplied);

            RenderBestMatch(summary.BestMatch);

            if (View.NextDayButton != null) View.NextDayButton.interactable = true;
        }

        private void OnNoResultAvailable()
        {
            SetActive(View.ErrorPanel, true);
            if (View.NextDayButton != null) View.NextDayButton.interactable = false;
            Debug.LogError("[ResultsWindow] no SalesDayResult - Results cannot proceed.");
        }

        private void RenderBestMatch(BestMatchCard best)
        {
            SetActive(View.BestMatchPanel, best != null);
            if (best == null) return;

            Set(View.BestBookLabel, best.BookId);
            Set(View.BestRequestLabel, best.RequestId);
            Set(View.BestTierLabel, $"{best.Tier} (score {best.Score})");

            if (View.BestReasonLabel == null) return;

            var parts = new List<string>(5);
            if (best.Reason != null)
            {
                if (best.Reason.MatchedGenres.Count > 0)
                    parts.Add($"genre({string.Join(",", best.Reason.MatchedGenres)})");
                if (best.Reason.MatchedTags.Count > 0)
                    parts.Add($"tags({string.Join(",", best.Reason.MatchedTags)})");
                if (best.Reason.MatchedMood.Count > 0)
                    parts.Add($"mood({string.Join(",", best.Reason.MatchedMood)})");
                if (best.Reason.PriceFits) parts.Add("price");
                if (best.Reason.LocationBonus) parts.Add("location");
            }

            View.BestReasonLabel.text = parts.Count > 0
                ? $"matched: {string.Join(", ", parts)}"
                : string.Empty;
        }

        private void OnNextDayClicked() => AdvanceToNextDayAsync().Forget();

        private async UniTaskVoid AdvanceToNextDayAsync()
        {
            if (_service == null) return;
            if (View.NextDayButton != null) View.NextDayButton.interactable = false;

            try
            {
                await _service.AdvanceToNextDayAsync(_cts.Token);
                await CloseAsync(_cts.Token);
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private void OnOpenNewspaperClicked() => OpenNewspaperAsync().Forget();

        private async UniTaskVoid OpenNewspaperAsync()
        {
            await UIManager.ShowAsync<NewspaperWindow>(ct: _cts.Token);
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
