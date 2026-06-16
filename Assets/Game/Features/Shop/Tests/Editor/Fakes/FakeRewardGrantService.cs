using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Rewards.API;

namespace Game.Shop.Tests.Editor.Fakes
{
    internal sealed class FakeRewardGrantService : IRewardGrantService
    {
        public List<(RewardSpec spec, string source)> GrantCalls { get; } = new();

        /// <summary>If non-null, returned instead of the requested spec on Success.</summary>
        public RewardSpec OverrideGranted { get; set; }

        /// <summary>If set, the next call returns a Fail with this reason.</summary>
        public string ForceFailureReason { get; set; }

        public UniTask<RewardGrantResult> GrantAsync(RewardSpec spec, string source, CancellationToken ct)
        {
            GrantCalls.Add((spec, source));
            if (ForceFailureReason != null)
                return UniTask.FromResult(RewardGrantResult.Fail(ForceFailureReason));
            return UniTask.FromResult(RewardGrantResult.Ok(OverrideGranted ?? spec));
        }
    }
}
