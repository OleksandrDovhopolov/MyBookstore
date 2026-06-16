using System.Collections.Generic;
using Book.Sell.Services;
using Game.Rewards.API;
using Game.Rewards.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — Shop, FTUE and Battle Pass consumers
    // all live across scene transitions, so the grant service must too).
    // Resolves from the same scope: IResourcesService, IInventoryService, IConfigsService, ISalesRandom.
    //
    // Expander list: explicit array via factory (one entry: BookBoxRewardExpander). VContainer does
    // not auto-resolve empty collections in this project; the explicit registration here is the
    // canonical pattern for adding more expanders in the future.
    public static class RewardsVContainerBindings
    {
        public static void RegisterRewards(this IContainerBuilder builder)
        {
            // RNG port — adapter over the project-wide ISalesRandom so every roll shares one source.
            builder.Register<IRewardRandom>(r => new SalesRandomAdapter(r.Resolve<ISalesRandom>()), Lifetime.Singleton);

            builder.Register<BookBoxRewardExpander>(Lifetime.Singleton);

            builder.Register<IReadOnlyList<IRewardSpecExpander>>(
                r => new IRewardSpecExpander[] { r.Resolve<BookBoxRewardExpander>() },
                Lifetime.Singleton);

            builder.Register<LocalRewardGrantService>(Lifetime.Singleton)
                .As<IRewardGrantService>();
        }

        private sealed class SalesRandomAdapter : IRewardRandom
        {
            private readonly ISalesRandom _inner;
            public SalesRandomAdapter(ISalesRandom inner) { _inner = inner; }
            public int Range(int minInclusive, int maxExclusive) => _inner.Range(minInclusive, maxExclusive);
            public double NextDouble() => _inner.NextDouble();
        }
    }
}
