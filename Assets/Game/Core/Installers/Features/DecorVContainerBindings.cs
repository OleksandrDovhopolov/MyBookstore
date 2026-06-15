using Book.Sell.API;
using Game.Decor;
using Game.Decor.Services;
using Game.Decor.UI;
using Game.Inventory.API;
using UnityEngine;
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

            // Config validation runs at boot. In Editor errors throw to block Play mode.
            builder.RegisterEntryPoint<DecorConfigValidator>(Lifetime.Singleton);

            // Debug placement screen — registered only when present in the scene.
            if (Object.FindAnyObjectByType<DecorPlacementScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<DecorPlacementScreenView>();
        }
    }
}
