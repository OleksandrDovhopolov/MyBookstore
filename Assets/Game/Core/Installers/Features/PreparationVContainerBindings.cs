using Book.Sell.Services;
using Game.Preparation.Services;
using Game.Preparation.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Main Feature (core loop) — Preparation phase. Registered in: GameInstaller (GameplayLifetimeScope).
    // Resolves from parent (GlobalLifetimeScope): ISaveService, IConfigsService.
    // Resolves from sibling fiches: IDayProgressService (DayCycle).
    //
    // Side effect: registers ISalesSetupProvider (PreparationSalesSetupProvider) here, replacing
    // the previous DefaultSalesSetupProvider registration in BookSellVContainerBindings. This keeps
    // Book.Sell unaware of Preparation while still letting Sales read the player's choice.
    public static class PreparationVContainerBindings
    {
        public static void RegisterPreparation(this IContainerBuilder builder)
        {
            builder.Register<IPreparationInventoryProvider, CatalogInventoryProvider>(Lifetime.Singleton);
            builder.Register<IPreparationSessionService, PreparationSessionService>(Lifetime.Singleton);
            builder.Register<ISalesSetupProvider, PreparationSalesSetupProvider>(Lifetime.Singleton);

            if (Object.FindAnyObjectByType<PreparationScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<PreparationScreenView>();
        }
    }
}
