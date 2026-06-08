using VContainer;

namespace Game.Bootstrap
{
    // Registered in: GameInstaller (GameplayLifetimeScope)
    // Resolves from scene scope: IInventoryService, IResourceManager
    public static class RewardDropVContainerBindings
    {
        public static void RegisterRewardDrop(this IContainerBuilder builder)
        {
            // TODO: Reward specs config (reward type → item/resource mapping — ScriptableObject)
            // builder.RegisterInstance(_rewardSpecsConfig);

            // TODO: Reward spec provider
            // builder.Register<IRewardSpecProvider>(_ => new RewardSpecProvider(rewardSpecsConfig), Lifetime.Singleton);

            // TODO: Reward handlers (one per reward type, all registered as IRewardHandler)
            // builder.Register<ResourceRewardHandler>(Lifetime.Singleton).As<IRewardHandler>();
            // builder.Register<InventoryRewardHandler>(Lifetime.Singleton).As<IRewardHandler>();

            // TODO: Player state snapshot handlers (capture / restore player state around a reward)
            // builder.Register<ResourcePlayerStateSnapshotHandler>(Lifetime.Singleton).As<IPlayerStateSnapshotHandler>();
            // builder.Register<InventoryPlayerStateSnapshotHandler>(Lifetime.Singleton).As<IPlayerStateSnapshotHandler>();
            // builder.Register<IPlayerStateSnapshotApplier, PlayerStateSnapshotApplier>(Lifetime.Singleton);

            // TODO: Reward server API and grant service
            // builder.Register<IRewardServerApi, HttpRewardServerApi>(Lifetime.Singleton);
            // builder.Register<IRewardGrantService, ServerRewardGrantService>(Lifetime.Singleton);

            // TODO: High-level orchestrator that applies reward responses
            // builder.Register<IRewardResponseApplier, RewardResponseApplier>(Lifetime.Singleton);
        }
    }
}
