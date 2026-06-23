using System;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Results.Domain;
using Save;
using UnityEngine;

namespace Game.DayCycle.Results.Services
{
    public class ResultsSummarySessionService : IResultsSessionService
    {
        private const string LogPrefix = "[Results]";

        private readonly ISaveService _save;
        private readonly IDayProgressService _dayProgress;
        private readonly IResultsSummaryBuilder _summaryBuilder;
        private readonly SemaphoreSlim _loadGate = new(1, 1);

        private ResultsSummary _currentSummary;
        private int? _advancedCompletedDay;
        private bool _advanceInProgress;

        public ResultsSummarySessionService(
            ISaveService save,
            IDayProgressService dayProgress,
            IResultsSummaryBuilder summaryBuilder)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _summaryBuilder = summaryBuilder ?? throw new ArgumentNullException(nameof(summaryBuilder));
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

                var summary = _summaryBuilder.Build(sales);

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
        }
    }
}
