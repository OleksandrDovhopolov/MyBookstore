using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class SalesGoldCollector : ISalesGoldCollector
    {
        private const string LogPrefix = "[Sales.Day]";

        private readonly IResourcesService _resources;
        private readonly List<UniTask> _pendingCommits = new();

        public SalesGoldCollector(IResourcesService resources = null)
        {
            _resources = resources;
        }

        public void Reset()
        {
            _pendingCommits.Clear();
        }

        public void CollectSaleGold(int day, string bookId, int amount, string source)
        {
            if (amount <= 0) return;

            var commit = CollectSaleGoldAsync(day, bookId, amount, source).Preserve();
            _pendingCommits.Add(commit);
            commit.Forget();
        }

        public async UniTask FlushAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_pendingCommits.Count == 0) return;

            try
            {
                await UniTask.WhenAll(_pendingCommits);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} failed while waiting for pending gold sale commits: {ex.Message}");
            }
            finally
            {
                _pendingCommits.Clear();
            }
        }

        private async UniTask CollectSaleGoldAsync(int day, string bookId, int amount, string source)
        {
            if (_resources == null)
            {
                Debug.LogError($"{LogPrefix} cannot collect {amount} gold for sold book '{bookId}' ({source}): IResourcesService is not available.");
                return;
            }

            try
            {
                await _resources.AddAsync(ResourceIds.Gold, amount, BuildReason(day, bookId, source), CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} failed to collect {amount} gold for sold book '{bookId}' ({source}): {ex.Message}");
            }
        }

        private static string BuildReason(int day, string bookId, string source)
            => $"sales_day_{day}_{source}_{bookId}";
    }

    public sealed class NoOpSalesGoldCollector : ISalesGoldCollector
    {
        public void Reset()
        {
        }

        public void CollectSaleGold(int day, string bookId, int amount, string source)
        {
        }

        public UniTask FlushAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }
    }
}
