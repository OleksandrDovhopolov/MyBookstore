using Book.Sell.API;
using Game.Conditions.API;
using Game.Decor;
using Game.Decor.Conditions;
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

            // DecorPlacementService registers its ISaveHook lazily in its constructor. It is
            // force-constructed before SaveDataLoadOperation via Bootstrap.Construct (IDecorPlacementService
            // is injected there purely to trigger construction) so AfterLoadAsync fires on load and
            // saved placement survives relaunch. Previously a P1 bug: without eager construction the
            // hook never ran and the next save overwrote the previous placement state.
            builder.Register<DecorPlacementService>(Lifetime.Singleton)
                .AsImplementedInterfaces() // exposes IDecorPlacementService + ISaveHook
                .AsSelf();                  // self resolution for DecorRewardService

            // Quest/unlock condition adapter ("decorEquipped"); discovered via the IConditionFactory collection.
            builder.Register<IConditionFactory, DecorEquippedConditionFactory>(Lifetime.Singleton);

            builder.Register<IDecorModifierProvider, ConfigBasedDecorModifierProvider>(Lifetime.Singleton);
            builder.Register<IInventoryItemUseHandler, DecorActivationUseHandler>(Lifetime.Singleton);
            builder.Register<IInventoryItemInfoProvider, DecorPlacementInfoProvider>(Lifetime.Singleton);

            // Config validation runs at boot. In Editor errors throw to block Play mode.
            builder.RegisterEntryPoint<DecorConfigValidator>(Lifetime.Singleton);

            // DecorPlacementWindow is loaded by AddressablesWindowFactory + injected via
            // IObjectResolver.Inject — no scene binding needed.
        }
    }
}
