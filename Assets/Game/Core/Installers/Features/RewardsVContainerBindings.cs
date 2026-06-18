using System.Collections.Generic;
using Game.Rewards.API;
using Game.Rewards.Services;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — Shop, FTUE and Battle Pass consumers
    // all live across scene transitions, so the grant service must too).
    // Resolves from the same scope: IResourcesService, IInventoryService, IConfigsService.
    //
    // Expander list: explicit array via factory (one entry: BookBoxRewardExpander). VContainer does
    // not auto-resolve empty collections in this project; the explicit registration here is the
    // canonical pattern for adding more expanders in the future.
    //
    // RNG: Rewards owns its random source (UnityRewardRandom). Reusing BookSell's ISalesRandom was
    // tempting for "one source of truth" but ISalesRandom lives in GameplayLifetimeScope (scene), and
    // Rewards is global — scope mismatch. Phase 0 accepts two RNG sources; tests still get
    // determinism through FakeRewardRandom.
    public static class RewardsVContainerBindings
    {
        public static void RegisterRewards(this IContainerBuilder builder)
        {
            builder.Register<IRewardRandom, UnityRewardRandom>(Lifetime.Singleton);

            builder.Register<BookBoxRewardExpander>(Lifetime.Singleton);

            builder.Register<IReadOnlyList<IRewardSpecExpander>>(
                r => new IRewardSpecExpander[] { r.Resolve<BookBoxRewardExpander>() },
                Lifetime.Singleton);

            builder.Register<LocalRewardGrantService>(Lifetime.Singleton)
                .As<IRewardGrantService>();
        }

        private sealed class UnityRewardRandom : IRewardRandom
        {
            public int Range(int minInclusive, int maxExclusive) =>
                Random.Range(minInclusive, maxExclusive);
            public double NextDouble() => Random.value;
        }
    }
}
