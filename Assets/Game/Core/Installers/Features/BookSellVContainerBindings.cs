using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Main Feature (core loop) — Sales phase, real-time customer simulation (ADR-0003).
    // Registered in: GameInstaller (GameplayLifetimeScope).
    // Resolves from parent (GlobalLifetimeScope): IConfigsService.
    // No ISaveService / IDayProgressService — Sales is standalone for this iteration.
    public static class BookSellVContainerBindings
    {
        public static void RegisterBookSell(this IContainerBuilder builder)
        {
            // Reused pure-domain services.
            builder.Register<ISalesRandom, UnityRandomSalesRandom>(Lifetime.Singleton);
            builder.Register<ISalesSetupProvider, DefaultSalesSetupProvider>(Lifetime.Singleton);
            builder.Register<IRecommendationScoringService, RecommendationScoringService>(Lifetime.Singleton);
            builder.Register<IPassiveSaleSelector, DefaultPassiveSaleSelector>(Lifetime.Singleton);

            // Customer simulation.
            builder.Register<IInteractionLock, InteractionLock>(Lifetime.Singleton);
            builder.Register<ICustomerSpawner, DefaultCustomerSpawner>(Lifetime.Singleton);
            builder.RegisterInstance(new SalesTuning());
            builder.Register<ISalesDayController, SalesDayController>(Lifetime.Singleton);

            // Debug screen. Registered only if present in the scene, so the project runs before the UI
            // is wired. Same pattern as MorningScreenView.
            if (Object.FindAnyObjectByType<SalesScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<SalesScreenView>();
        }
    }
}
