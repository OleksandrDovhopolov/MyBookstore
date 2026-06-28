using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.SalesStats.API
{
    /// <summary>
    /// Persistence seam under <see cref="ISalesStatsService"/>. MVP is local (save-module backed);
    /// a future server implementation swaps in via DI. Mirrors <c>IProgressionRepository</c>.
    /// </summary>
    public interface ISalesStatsRepository
    {
        UniTask<SalesStatsStateDto> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(SalesStatsStateDto state, CancellationToken ct);
    }
}
