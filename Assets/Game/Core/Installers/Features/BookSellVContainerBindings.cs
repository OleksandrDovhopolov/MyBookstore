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
        public static void RegisterBookSell(this IContainerBuilder builder, CustomerVisual customerVisualPrefab, Transform customerSpawnRoot)
        {
            // Reused pure-domain services.
            builder.Register<ISalesRandom, UnityRandomSalesRandom>(Lifetime.Singleton);
            // ISalesSetupProvider is registered by Preparation (PreparationSalesSetupProvider),
            // which reads the player's choice from preparation.session and falls back to the
            // catalog if no session exists yet.
            builder.Register<IRecommendationScoringService, RecommendationScoringService>(Lifetime.Singleton);

            // Probabilistic passive sale (ADR-0004): chance gate per genre, then weighted pick by RarityWeight.
            // Decor stays a stub (NoopDecorModifierProvider returns 1.0); real decor lands in a follow-up.
            builder.Register<IDecorModifierProvider, NoopDecorModifierProvider>(Lifetime.Singleton);
            builder.Register<IBaseSaleChanceCalculator, EconomyBasedSaleChanceCalculator>(Lifetime.Singleton);
            builder.Register<IPassiveSaleSelector, WeightedPassiveSaleSelector>(Lifetime.Singleton);

            // Customer simulation.
            builder.Register<IInteractionLock, InteractionLock>(Lifetime.Singleton);
            builder.Register<ICustomerSpawner, DefaultCustomerSpawner>(Lifetime.Singleton);
            builder.RegisterInstance(new SalesTuning());
            builder.Register<ISalesDayController, SalesDayController>(Lifetime.Singleton);

            // Customer visualization + world-space thought bubbles (Phase 0 of World HUD).
            builder.RegisterInstance(new CustomerVisualRegistryConfig(customerVisualPrefab, customerSpawnRoot));
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
