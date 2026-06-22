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
    // Registered in: LocationInstaller (LocationLifetimeScope — LocationScene, additive).
    // Resolves from parent (GlobalLifetimeScope): IConfigsService, ISaveService, IDecorPlacementService.
    // ISalesSetupProvider is registered alongside in LocationInstaller (PreparationSalesSetupProvider),
    // which reads the player's choice from the preparation.session save module.
    public static class BookSellVContainerBindings
    {
        // Shared save-backed shelf-session state. Used in two scopes:
        //   - hub (Preparation): preserves previous shelf survivors for continuity/restock;
        //   - location (Sales): marks books sold during the current sales session for UI/day flow.
        // Ownership truth lives in inventory; this service is registered globally so both scopes share one instance.
        public static void RegisterBookSellSharedState(this IContainerBuilder builder)
        {
            builder.Register<ISalesShelfStateService, SalesShelfStateService>(Lifetime.Singleton);
        }

        public static void RegisterBookSell(
            this IContainerBuilder builder,
            CustomerVisual customerVisualPrefab,
            Transform customerSpawnRoot,
            Transform customerEntryLeft = null,
            Transform customerEntryRight = null,
            Transform customerShopApproach = null,
            Transform[] customerLaneAnchors = null,
            Transform customerExitLeft = null,
            Transform customerExitRight = null,
            SalesTuningConfig salesTuningConfig = null)
        {
            // Reused pure-domain services.
            builder.Register<ISalesRandom, UnityRandomSalesRandom>(Lifetime.Singleton);
            // ISalesSetupProvider is registered in LocationInstaller (PreparationSalesSetupProvider),
            // which reads the player's choice from preparation.session and falls back to the
            // catalog if no session exists yet.
            builder.Register<IRecommendationScoringService, RecommendationScoringService>(Lifetime.Singleton);

            // Probabilistic passive sale (ADR-0004): chance gate per genre, then weighted pick by RarityWeight.
            // IDecorModifierProvider is registered by RegisterDecor in Game.Decor module.
            builder.Register<IBaseSaleChanceCalculator, EconomyBasedSaleChanceCalculator>(Lifetime.Singleton);
            builder.Register<IPassiveSaleSelector, WeightedPassiveSaleSelector>(Lifetime.Singleton);
            // ISalesShelfStateService НЕ здесь — он общий для хаба (Preparation) и локации (Sales),
            // регистрируется глобально через RegisterBookSellSharedState. См. ниже.

            // Customer simulation.
            builder.Register<IInteractionLock, InteractionLock>(Lifetime.Singleton);
            
            
            //builder.Register<ICustomerSpawner, DefaultCustomerSpawner>(Lifetime.Singleton);
            //builder.Register<ICustomerSpawner, FifteenCustomersSinglePassiveAttemptSpawner>(Lifetime.Singleton); //TEST was created to test zero books selected
            //builder.Register<ICustomerSpawner, ActiveRequestsOnlyCustomerSpawner>(Lifetime.Singleton); //TEST 3-5 active-request-only customers (1 request each)
            //builder.Register<ICustomerSpawner, OneToThreePassiveAttemptsCustomerSpawner>(Lifetime.Singleton); //TEST 1-N passive purchases
            builder.Register<ICustomerSpawner, TenCustomersThreeActiveAfterPassiveSpawner>(Lifetime.Singleton); //TEST 10 customers, 1-2 passive each; first 3 also active after passive
            //builder.Register<ICustomerSpawner, TenCustomersThreeActiveBetweenPassivesSpawner>(Lifetime.Singleton); //TEST 10 customers, 1-2 passive each; first 3: passive -> active -> 1 passive
            
            
            // Tuning comes from a designer-editable SO when assigned; otherwise code defaults.
            builder.RegisterInstance(salesTuningConfig != null ? salesTuningConfig.BuildTuning() : new SalesTuning());
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

            // Opens RecommendationMinigameWindow on active requests and pauses the day while it is up.
            // IUIManager resolves from the parent (bootstrap) scope; the controller is passed via WindowArgs.
            builder.RegisterEntryPoint<RecommendationMinigamePresenter>(Lifetime.Singleton)
                .AsSelf()
                .As<IRecommendationMinigamePresenter>();

            // Debug screen. Registered only if present in the scene, so the project runs before the UI
            // is wired. Same pattern as MorningScreenView.
            if (Object.FindAnyObjectByType<SalesScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<SalesScreenView>();
        }
    }
}
