using System.Collections.Generic;
using System.Threading;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;

namespace Book.Sell.Tests.Editor.Fakes
{
    public sealed class RecordingShelfStateService : ISalesShelfStateService
    {
        private readonly SalesShelfState _state = new();

        public List<string> Sold { get; } = new();

        public IReadOnlyList<string> ShelfBookIds => _state.ShelfBookIds;

        public SalesShelfState CurrentState => _state;

        public bool IsSold(string bookId) => Sold.Contains(bookId);

        public UniTask SetShelfAsync(IReadOnlyList<string> bookIds, CancellationToken ct)
        {
            _state.ShelfBookIds = new List<string>(bookIds ?? System.Array.Empty<string>());
            return UniTask.CompletedTask;
        }

        public UniTask MarkSoldAsync(string bookId, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(bookId) && !Sold.Contains(bookId))
                Sold.Add(bookId);
            return UniTask.CompletedTask;
        }
    }
}
