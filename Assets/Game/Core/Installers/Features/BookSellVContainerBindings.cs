using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.UI;
using Book.Sell.UI.Customer;
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
        public static void RegisterBookSell(
            this IContainerBuilder builder,
            CustomerVisual customerVisualPrefab,
            Transform customerSpawnRoot,
            Transform customerEntryLeft = null,
            Transform customerEntryRight = null,
            Transform customerShopApproach = null,
            Transform[] customerLaneAnchors = null,
            Transform customerExitLeft = null,
            Transform customerExitRight = null)
        {
            // Reused pure-domain services.
            builder.Register<ISalesRandom, UnityRandomSalesRandom>(Lifetime.Singleton);
            // ISalesSetupProvider is registered by Preparation (PreparationSalesSetupProvider),
            // which reads the player's choice from preparation.session and falls back to the
            // catalog if no session exists yet.
            builder.Register<IRecommendationScoringService, RecommendationScoringService>(Lifetime.Singleton);

            // Probabilistic passive sale (ADR-0004): chance gate per genre, then weighted pick by RarityWeight.
            // IDecorModifierProvider is registered by RegisterDecor in Game.Decor module.
            builder.Register<IBaseSaleChanceCalculator, EconomyBasedSaleChanceCalculator>(Lifetime.Singleton);
            builder.Register<IPassiveSaleSelector, WeightedPassiveSaleSelector>(Lifetime.Singleton);

            // Customer simulation.
            builder.Register<IInteractionLock, InteractionLock>(Lifetime.Singleton);
            builder.Register<ICustomerSpawner, DefaultCustomerSpawner>(Lifetime.Singleton);
            builder.RegisterInstance(new SalesTuning());
            builder.Register<ISalesDayController, SalesDayController>(Lifetime.Singleton);

            // Customer visualization + world-space thought bubbles (Phase 0 of World HUD).
            builder.RegisterInstance(new CustomerVisualRegistryConfig(
                customerVisualPrefab,
                customerSpawnRoot,
                customerEntryLeft,
                customerEntryRight,
                customerShopApproach,
                customerLaneAnchors,
                customerExitLeft,
                customerExitRight));
            builder.Register<CustomerVisualRegistry>(Lifetime.Singleton)
                .AsImplementedInterfaces() // exposes ICustomerVisualRegistry, IStartable, IDisposable
                .AsSelf();
            builder.RegisterEntryPoint<CustomerBubbleBinder>(Lifetime.Singleton);

            // Debug screen. Registered only if present in the scene, so the project runs before the UI
            // is wired. Same pattern as MorningScreenView.
            if (Object.FindAnyObjectByType<SalesScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<SalesScreenView>();
        }
    }
}
