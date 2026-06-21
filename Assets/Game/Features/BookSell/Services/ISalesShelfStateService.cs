using System.Collections.Generic;
using System.Threading;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;

namespace Book.Sell.Services
{
    public interface ISalesShelfStateService
    {
        IReadOnlyList<string> ShelfBookIds { get; }

        SalesShelfState CurrentState { get; }

        bool IsSold(string bookId);

        UniTask SetShelfAsync(IReadOnlyList<string> bookIds, CancellationToken ct);

        UniTask MarkSoldAsync(string bookId, CancellationToken ct);
    }
}
