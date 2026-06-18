using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Rewards.API
{
    /// <summary>
    /// Single entry point for handing rewards to the player. Callers (Shop, FTUE, future BattlePass)
    /// pass a <see cref="RewardSpec"/>; the implementation expands it through registered
    /// <see cref="IRewardSpecExpander"/>s and writes the result to player state.
    /// </summary>
    /// <remarks>
    /// Phase 0: <c>LocalRewardGrantService</c> dispatches to <c>IResourcesService</c> and
    /// <c>IInventoryService</c> directly. Phase 1+: <c>ServerRewardGrantService</c> calls
    /// <c>POST /api/v1/rewards/grant</c> and applies the returned snapshot.
    /// </remarks>
    public interface IRewardGrantService
    {
        /// <summary>
        /// Grants <paramref name="spec"/> to the player. <paramref name="source"/> is an audit string
        /// recorded with each underlying mutation (e.g. <c>"shop:newspaper_book_common_15"</c>).
        /// Result's <c>Granted</c> contains the post-expansion spec — UI should render that, not the
        /// input.
        /// </summary>
        UniTask<RewardGrantResult> GrantAsync(RewardSpec spec, string source, CancellationToken ct);
    }
}
