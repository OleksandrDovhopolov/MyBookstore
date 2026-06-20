using System;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Game.DayCycle.Results.Domain;
using Game.Progression.API;
using Game.Resources.API;
using Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.DayCycle.Results.Services
{
    /// <inheritdoc cref="IResultsSessionService"/>
    public sealed class ResultsSessionService : IResultsSessionService
    {
        private const string LogPrefix = "[Results]";
        public const string AppliedRewardsModuleKey = "results.applied_rewards";
        public const int AppliedRewardsSchemaVersion = 1;

        private readonly ISaveService _save;
        private readonly IDayProgressService _dayProgress;
        private readonly IResourcesService _resources;
        private readonly IProgressionService _progression;
        private readonly IResultsRewardService _rewards;
        private readonly IResultsReviewTextProvider _reviewProvider;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly SemaphoreSlim _loadGate = new(1, 1);

        private ResultsSummary _currentSummary;
        private int? _advancedCompletedDay;
        private bool _advanceInProgress;

        public ResultsSessionService(
            ISaveService save,
            IDayProgressService dayProgress,
            IResourcesService resources,
            IProgressionService progression,
            IResultsRewardService rewards,
            IResultsReviewTextProvider reviewProvider,
            ISceneTransitionService sceneTransition)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _reviewProvider = reviewProvider ?? throw new ArgumentNullException(nameof(reviewProvider));
            _sceneTransition = sceneTransition ?? throw new ArgumentNullException(nameof(sceneTransition));
        }

        public ResultsSummary CurrentSummary => _currentSummary;

        public event Action<ResultsSummary> SummaryReady;
        public event Action NoResultAvailable;

        public async UniTask LoadAndApplyAsync(CancellationToken ct)
        {
            await _loadGate.WaitAsync(ct);
            try
            {
                var sales = await _save.GetModuleAsync<SalesDayResult>(
                    SalesSaveKeys.LastDayResult, ct);

                if (sales == null)
                {
                    Debug.LogWarning($"{LogPrefix} no SalesDayResult in save module " +
                                     $"'{SalesSaveKeys.LastDayResult}'.");
                    NoResultAvailable?.Invoke();
                    return;
                }

                if (_currentSummary != null && _currentSummary.Day == sales.Day)
                {
                    SummaryReady?.Invoke(_currentSummary);
                    return;
                }

                // Block autosave for the full apply + persist sequence so a debounced write cannot
                // sneak between balance mutation and idempotency record persistence.
                using var lease = _save.BlockAutosave();

                var applied = await _save.GetModuleAsync<ResultsRewardsState>(AppliedRewardsModuleKey, ct)
                              ?? new ResultsRewardsState();

                var alreadyAppliedForDay = applied.AppliedDays.Find(a => a.CompletedDay == sales.Day);
                ResultsSummary summary;

                if (alreadyAppliedForDay != null)
                {
                    Debug.Log($"{LogPrefix} day {sales.Day} already applied - rebuilding summary from stored deltas.");
                    summary = BuildSummary(sales, alreadyAppliedForDay.GoldDelta,
                        alreadyAppliedForDay.ReputationDelta, alreadyApplied: true);
                }
                else
                {
                    var computation = _rewards.Compute(sales, _progression.Reputation);

                    var reason = $"results_day_{sales.Day}";
                    await _resources.AddAsync(ResourceIds.Gold, computation.GoldDelta, reason, ct);
                    await _progression.AddReputationAsync(computation.ReputationDelta, reason, ct);

                    applied.AppliedDays.Add(new AppliedDayRewards
                    {
                        CompletedDay = sales.Day,
                        GoldDelta = computation.GoldDelta,
                        ReputationDelta = computation.ReputationDelta,
                        AppliedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                    await _save.UpdateModuleAsync(AppliedRewardsModuleKey, applied,
                        AppliedRewardsSchemaVersion, ct);

                    Debug.Log($"{LogPrefix} day {sales.Day} applied: " +
                              $"+{computation.GoldDelta} gold, +{computation.ReputationDelta} reputation.");
                    summary = BuildSummary(sales, computation.GoldDelta,
                        computation.ReputationDelta, alreadyApplied: false);
                    summary.BestMatch = computation.BestMatch;
                }

                // Best-match for the already-applied path: recompute from sales (no balance side-effect).
                if (summary.BestMatch == null)
                    summary.BestMatch = _rewards.Compute(sales, _progression.Reputation).BestMatch;

                await _dayProgress.MarkCurrentDayCompletedAsync(ct);
                Debug.Log($"{LogPrefix} day {sales.Day} marked completed; phase={_dayProgress.Current.CurrentPhase}.");

                _currentSummary = summary;
                SummaryReady?.Invoke(summary);
            }
            finally
            {
                _loadGate.Release();
            }
        }

        public async UniTask AdvanceToNextDayAsync(CancellationToken ct)
        {
            if (_advanceInProgress)
            {
                Debug.Log($"{LogPrefix} advance ignored: already in progress.");
                return;
            }

            var completedDay = _currentSummary?.Day;
            if (completedDay == null)
            {
                var sales = await _save.GetModuleAsync<SalesDayResult>(
                    SalesSaveKeys.LastDayResult, ct);
                completedDay = sales?.Day;
            }

            if (completedDay == null)
            {
                Debug.LogWarning($"{LogPrefix} cannot advance: no completed SalesDayResult.");
                return;
            }

            Debug.Log($"{LogPrefix} advance requested: completed day {completedDay.Value}, current day {_dayProgress.Current.CurrentDay}, phase={_dayProgress.Current.CurrentPhase}.");

            if (_advancedCompletedDay == completedDay)
            {
                Debug.Log($"{LogPrefix} advance ignored: day {completedDay.Value} already advanced in this session.");
                return;
            }

            if (_dayProgress.Current.CurrentDay != completedDay.Value)
            {
                Debug.LogWarning($"{LogPrefix} skip advance for completed day {completedDay.Value}: " +
                                 $"current day is already {_dayProgress.Current.CurrentDay}.");
                _advancedCompletedDay = completedDay;
                return;
            }

            _advanceInProgress = true;
            try
            {
                using (var lease = _save.BlockAutosave())
                {
                    await _dayProgress.AdvanceToNextDayAsync(ct);
                }

                _advancedCompletedDay = completedDay;
                Debug.Log($"{LogPrefix} advanced to day {_dayProgress.Current.CurrentDay}, phase={_dayProgress.Current.CurrentPhase}.");
            }
            finally
            {
                _advanceInProgress = false;
            }

            // Reload the active gameplay scene so all per-scene state (Sales screen, customers,
            // etc.) is freshly recreated under the new CurrentDay.
            var sceneName = SceneManager.GetActiveScene().name;
            await _sceneTransition.TransitionToAsync(sceneName, progress: null, ct);
        }

        private ResultsSummary BuildSummary(SalesDayResult sales, int goldDelta, int repDelta, bool alreadyApplied)
            => new()
            {
                Day = sales.Day,
                SalesCount = sales.SalesCount,
                GoldEarned = sales.GoldEarned,
                ExcellentCount = sales.ExcellentCount,
                NormalCount = sales.NormalCount,
                FailedCount = sales.FailedCount,
                SkippedCount = sales.SkippedCount,
                ReviewText = _reviewProvider.Pick(sales),
                GoldDelta = goldDelta,
                ReputationDelta = repDelta,
                AlreadyApplied = alreadyApplied
            };
    }
}
