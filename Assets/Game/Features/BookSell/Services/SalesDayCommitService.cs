using System;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.Inventory.API;
using Game.Resources.API;
using Game.SalesStats.API;
using Save;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Default <see cref="ISalesDayCommitService"/>. Applies the whole day result in memory under a
    /// single <see cref="ISaveService.BlockAutosave"/> lease, then forces one save — so a crash before
    /// that save persists nothing (clean rollback), and a completed day is applied exactly once.
    /// Also marks the day completed (<c>day_progress.CompletedDays</c>) as part of the same commit, so
    /// the day can never be replayed for a second grant.
    /// </summary>
    public sealed class SalesDayCommitService : ISalesDayCommitService
    {
        private const string LogPrefix = "[Sales.Commit]";

        private readonly ISaveService _save;
        private readonly IResourcesService _resources;
        private readonly IInventoryService _inventory;
        private readonly ISalesShelfStateService _shelfState;
        private readonly ISalesStatsRecorder _salesStats;
        private readonly IDayProgressService _dayProgress;

        public SalesDayCommitService(
            ISaveService save,
            IResourcesService resources,
            IInventoryService inventory,
            ISalesShelfStateService shelfState,
            ISalesStatsRecorder salesStats,
            IDayProgressService dayProgress)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _shelfState = shelfState ?? throw new ArgumentNullException(nameof(shelfState));
            _salesStats = salesStats ?? throw new ArgumentNullException(nameof(salesStats));
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
        }

        public async UniTask CommitAsync(SalesDayResult result, CancellationToken ct)
        {
            if (result == null) return;

            // Idempotent: a completed day's effects are applied once. Replay is also prevented upstream
            // (StartOrResume routes a completed day to Results, not back to Sales).
            if (_dayProgress.Current.CompletedDays.Contains(result.Day))
            {
                Debug.Log($"{LogPrefix} day {result.Day} already committed — skipping.");
                return;
            }

            var reason = $"sales_day_{result.Day}";

            // All mutations are in-memory under one autosave-block; the single forced save below writes
            // the whole consistent snapshot at once. Nothing reaches disk before that point.
            using (_save.BlockAutosave())
            {
                if (result.GoldEarned > 0)
                    await _resources.AddAsync(ResourceIds.Gold, result.GoldEarned, reason, ct);

                if (result.SoldBookIds != null)
                {
                    foreach (var bookId in result.SoldBookIds)
                    {
                        if (string.IsNullOrEmpty(bookId)) continue;

                        var removed = await _inventory.RemoveAsync(bookId, 1, ct);
                        if (!removed)
                            Debug.LogError($"{LogPrefix} sold book '{bookId}' not present in inventory at commit (day {result.Day}).");

                        await _shelfState.MarkSoldAsync(bookId, ct);
                        _salesStats.RecordSold(bookId, new SaleContext(result.LocationId, result.Day));
                    }
                }

                await _save.UpdateModuleAsync(SalesSaveKeys.LastDayResult, result,
                    SalesSaveKeys.LastDayResultSchemaVersion, ct);

                // Persist completion for anti-replay/atomicity, but DO NOT fire the phase→Results
                // transition here. That live UI routing is owned by the Results flow
                // (ResultsSummarySessionService), which runs after ResultsWindow is shown. Firing
                // PhaseChanged during the commit makes HubPhaseRouter open ResultsWindow prematurely
                // (still in the location), double-opening it alongside SalesScreenView's own show.
                // StartOrResume routes a day already in CompletedDays to Results on the next boot, so
                // anti-replay does not need the phase set here.
                if (!_dayProgress.Current.CompletedDays.Contains(result.Day))
                {
                    _dayProgress.Current.CompletedDays.Add(result.Day);
                    await _dayProgress.SaveAsync(ct);
                }
            }

            await _save.SaveAsync(ct, SaveMode.ForceWithSync);

            Debug.Log($"{LogPrefix} committed day {result.Day}: gold={result.GoldEarned}, " +
                      $"books={result.SoldBookIds?.Count ?? 0}.");
        }
    }
}
