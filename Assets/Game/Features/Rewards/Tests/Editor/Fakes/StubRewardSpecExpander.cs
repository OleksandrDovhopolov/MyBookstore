using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Rewards.API;

namespace Game.Rewards.Tests.Editor.Fakes
{
    internal sealed class StubRewardSpecExpander : IRewardSpecExpander
    {
        private readonly Predicate<RewardSpec> _matches;
        private readonly Func<RewardSpec, RewardSpec> _expand;

        public int ExpandCalls { get; private set; }

        public StubRewardSpecExpander(Predicate<RewardSpec> matches, Func<RewardSpec, RewardSpec> expand)
        {
            _matches = matches;
            _expand = expand;
        }

        public bool CanExpand(RewardSpec spec) => _matches(spec);

        public UniTask<RewardSpec> ExpandAsync(RewardSpec spec, CancellationToken ct)
        {
            ExpandCalls++;
            return UniTask.FromResult(_expand(spec));
        }
    }
}
