using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class SoldBookCommitter : ISoldBookCommitter
    {
        private const string LogPrefix = "[Sales.Day]";

        private readonly IInventoryService _inventory;
        private readonly ISalesShelfStateService _shelfState;
        private readonly List<UniTask> _pendingCommits = new();

        public SoldBookCommitter(
            IInventoryService inventory = null,
            ISalesShelfStateService shelfState = null)
        {
            _inventory = inventory;
            _shelfState = shelfState;
        }

        public void Reset()
        {
            _pendingCommits.Clear();
        }

        public void CommitSoldBook(string bookId, string source)
        {
            var commit = CommitSoldBookAsync(bookId, source).Preserve();
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
                Debug.LogError($"{LogPrefix} failed while waiting for pending inventory sale commits: {ex.Message}");
            }
            finally
            {
                _pendingCommits.Clear();
            }
        }

        private async UniTask CommitSoldBookAsync(string bookId, string source)
        {
            if (string.IsNullOrEmpty(bookId)) return;

            if (_shelfState != null)
            {
                try
                {
                    await _shelfState.MarkSoldAsync(bookId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogPrefix} failed to mark sold shelf state for '{bookId}' ({source}): {ex.Message}");
                }
            }

            if (_inventory == null)
            {
                Debug.LogError($"{LogPrefix} cannot remove sold book '{bookId}' from inventory ({source}): IInventoryService is not available.");
                return;
            }

            try
            {
                var removed = await _inventory.RemoveAsync(bookId, 1, CancellationToken.None);
                if (!removed)
                    Debug.LogError($"{LogPrefix} sold book '{bookId}' was not present in inventory during {source} sale.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} failed to remove sold book '{bookId}' from inventory ({source}): {ex.Message}");
            }
        }
    }

    public sealed class NoOpSoldBookCommitter : ISoldBookCommitter
    {
        public void Reset()
        {
        }

        public void CommitSoldBook(string bookId, string source)
        {
        }

        public UniTask FlushAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }
    }
}
