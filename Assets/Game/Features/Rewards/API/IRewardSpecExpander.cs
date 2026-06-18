using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Rewards.API
{
    /// <summary>
    /// Plug-in slot for turning an abstract <see cref="RewardSpec"/> into a concrete one before dispatch.
    /// Phase 0 use case: <c>BookBoxRewardExpander</c> (PR4) maps <c>book_box_common_15</c> into 15
    /// concrete book items. Any number of expanders may be registered; the first whose
    /// <see cref="CanExpand"/> returns true wins.
    /// </summary>
    public interface IRewardSpecExpander
    {
        bool CanExpand(RewardSpec spec);
        UniTask<RewardSpec> ExpandAsync(RewardSpec spec, CancellationToken ct);
    }
}
