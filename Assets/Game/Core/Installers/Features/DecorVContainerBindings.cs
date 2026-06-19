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

            //TODO check bug
            /*
             * P1 Badge Eagerly register the decor save hook

DecorPlacementService only calls save.RegisterHook(this) from its constructor, but this registration is lazy and nothing in the bootstrap path resolves IDecorPlacementService before SaveDataLoadOperation;
 the first consumers I found are gameplay/preparation UI after boot. On a subsequent launch, saved placements are never loaded, 
 so active decor is empty and the next placement save can overwrite the previous placement state. 
Force this service to be constructed before save load (for example via the bootstrap forced-resolution list or a build callback).
             */
            
            builder.Register<DecorPlacementService>(Lifetime.Singleton)
                .AsImplementedInterfaces() // exposes IDecorPlacementService + ISaveHook
                .AsSelf();                  // self resolution for DecorRewardService

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
