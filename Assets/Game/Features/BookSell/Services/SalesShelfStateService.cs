using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Save;

namespace Book.Sell.Services
{
    public sealed class SalesShelfStateService : ISalesShelfStateService
    {
        private readonly ISaveService _save;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private SalesShelfState _state;

        public SalesShelfStateService(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public IReadOnlyList<string> ShelfBookIds => EnsureLoaded().ShelfBookIds;

        public SalesShelfState CurrentState => EnsureLoaded();

        public bool IsSold(string bookId)
        {
            if (string.IsNullOrEmpty(bookId)) return false;
            return EnsureLoaded().SoldBookIds.Contains(bookId);
        }

        public async UniTask SetShelfAsync(IReadOnlyList<string> bookIds, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var state = await LoadAsync(ct);
                var sold = new HashSet<string>(state.SoldBookIds ?? new List<string>(), StringComparer.Ordinal);
                state.ShelfBookIds = DistinctValid(bookIds, skip: sold);
                await SaveAsync(state, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask MarkSoldAsync(string bookId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(bookId)) return;

            await _gate.WaitAsync(ct);
            try
            {
                var state = await LoadAsync(ct);
                state.ShelfBookIds ??= new List<string>();
                state.SoldBookIds ??= new List<string>();

                state.ShelfBookIds.RemoveAll(id => string.Equals(id, bookId, StringComparison.Ordinal));

                if (!state.SoldBookIds.Contains(bookId))
                    state.SoldBookIds.Add(bookId);

                await SaveAsync(state, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        private SalesShelfState EnsureLoaded()
        {
            if (_state != null) return _state;

            _state = _save.GetModuleAsync<SalesShelfState>(SalesSaveKeys.ShelfState, CancellationToken.None)
                .GetAwaiter().GetResult() ?? new SalesShelfState();
            Normalize(_state);
            return _state;
        }

        private async UniTask<SalesShelfState> LoadAsync(CancellationToken ct)
        {
            if (_state != null) return _state;

            _state = await _save.GetModuleAsync<SalesShelfState>(SalesSaveKeys.ShelfState, ct)
                ?? new SalesShelfState();
            Normalize(_state);
            return _state;
        }

        private async UniTask SaveAsync(SalesShelfState state, CancellationToken ct)
        {
            Normalize(state);
            _state = state;
            await _save.UpdateModuleAsync(SalesSaveKeys.ShelfState, state, SalesSaveKeys.ShelfStateSchemaVersion, ct);
        }

        private static void Normalize(SalesShelfState state)
        {
            state.SoldBookIds = DistinctValid(state.SoldBookIds);
            var sold = new HashSet<string>(state.SoldBookIds, StringComparer.Ordinal);
            state.ShelfBookIds = DistinctValid(state.ShelfBookIds, sold);
        }

        private static List<string> DistinctValid(IReadOnlyList<string> ids, ISet<string> skip = null)
        {
            var result = new List<string>();
            if (ids == null) return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (skip != null && skip.Contains(id)) continue;
                if (seen.Add(id)) result.Add(id);
            }
            return result;
        }
    }
}
