using System;
using System.Collections.Generic;
using Game.Rewards.API;
using Game.Rewards.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — Shop, FTUE and Battle Pass consumers
    // all live across scene transitions, so the grant service must too).
    // Resolves from the same scope: IResourcesService, IInventoryService.
    //
    // Expanders: registered as an explicit IReadOnlyList<IRewardSpecExpander>. PR1 ships an empty
    // list; PR4 replaces the registration with an array containing BookBoxRewardExpander. VContainer
    // does not auto-resolve empty collections in this project, so an explicit instance is used.
    public static class RewardsVContainerBindings
    {
        public static void RegisterRewards(this IContainerBuilder builder)
        {
            builder.RegisterInstance<IReadOnlyList<IRewardSpecExpander>>(Array.Empty<IRewardSpecExpander>());

            builder.Register<LocalRewardGrantService>(Lifetime.Singleton)
                .As<IRewardGrantService>();
        }
    }
}
