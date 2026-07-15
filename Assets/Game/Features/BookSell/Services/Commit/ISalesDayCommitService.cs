using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;

namespace Book.Sell.Services
{
    /// <summary>
    /// Applies a completed Sales day's provisional effects (earned gold, sold books, shelf state,
    /// sales stats, <c>last_day_result</c>, and day completion) in one atomic commit. During the day
    /// nothing is persisted — exiting before commit rolls everything back. See docs/SAVE_DAY_FLOW.md.
    /// </summary>
    public interface ISalesDayCommitService
    {
        UniTask CommitAsync(SalesDayResult result, CancellationToken ct);
    }
}
