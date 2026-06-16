using Book.Sell.API;
using Game.Decor;
using Game.Decor.Services;
using Game.Inventory.API;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope).
    // Must run BEFORE RegisterBookSell so EconomyBasedSaleChanceCalculator resolves
    // IDecorModifierProvider against our ConfigBasedDecorModifierProvider (not the deleted Noop).
    // Resolves from parent: IConfigsService, ISaveService, IInventoryService, IResourcesService.
    public static class DecorVContainerBindings
    {
        public static void RegisterDecor(this IContainerBuilder builder)
        {
            builder.Register<SaveBackedDecorPlacementStorage>(Lifetime.Singleton);

            builder.Register<DecorPlacementService>(Lifetime.Singleton)
                .AsImplementedInterfaces() // exposes IDecorPlacementService + ISaveHook
                .AsSelf();                  // self resolution for DecorRewardService

            builder.Register<IDecorModifierProvider, ConfigBasedDecorModifierProvider>(Lifetime.Singleton);
            builder.Register<IDecorRewardService, DecorRewardService>(Lifetime.Singleton);
            builder.Register<IInventoryItemUseHandler, DecorActivationUseHandler>(Lifetime.Singleton);
            builder.Register<IInventoryItemInfoProvider, DecorPlacementInfoProvider>(Lifetime.Singleton);

            // Config validation runs at boot. In Editor errors throw to block Play mode.
            builder.RegisterEntryPoint<DecorConfigValidator>(Lifetime.Singleton);

            // DecorPlacementWindow is loaded by AddressablesWindowFactory + injected via
            // IObjectResolver.Inject — no scene binding needed.
        }
    }
}
