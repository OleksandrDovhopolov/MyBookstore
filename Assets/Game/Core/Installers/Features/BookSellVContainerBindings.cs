using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Services.Director;
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

        // Passive sales v2 (requested-genre): each customer rolls one genre from its profile.
        // Both hit and miss carry the chosen genre. Default model.
        public static void RegisterRequestedGenrePassiveSales(this IContainerBuilder builder)
        {
            builder.Register<IPassivePurchaseResolver, RequestedGenrePassiveResolver>(Lifetime.Singleton);
        }

        // Legacy passive (ADR-0004 shelf-roll): kept behind the seam for rollback. Not called by default.
        public static void RegisterLegacyPassiveSales(this IContainerBuilder builder)
        {
            builder.Register<IPassiveSaleSelector, WeightedPassiveSaleSelector>(Lifetime.Singleton);
            builder.Register<IPassivePurchaseResolver, LegacyShelfPassiveResolver>(Lifetime.Singleton);
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

            // Passive sale chance gate (ADR-0004). IDecorModifierProvider is registered by RegisterDecor.
            builder.Register<IBaseSaleChanceCalculator, EconomyBasedSaleChanceCalculator>(Lifetime.Singleton);
            // Per-customer desire profile — used by the spawner in both passive models.
            builder.Register<ICustomerProfileProvider, LocationDemandProfileProvider>(Lifetime.Singleton);
            // Passive model behind the IPassivePurchaseResolver seam. Default = requested-genre (v2).
            // To roll back to the old shelf-roll model, call RegisterLegacyPassiveSales(builder) instead.
            RegisterRequestedGenrePassiveSales(builder);
            // ISalesShelfStateService НЕ здесь — он общий для хаба (Preparation) и локации (Sales),
            // регистрируется глобально через RegisterBookSellSharedState. См. ниже.

            // Customer simulation.
            builder.Register<IInteractionLock, InteractionLock>(Lifetime.Singleton);
            builder.Register<IPassiveSaleRule, PassiveSaleCommentRule>(Lifetime.Singleton);
            builder.Register<ICustomerDirector>(
                resolver => new CustomerDirector(new[] { resolver.Resolve<IPassiveSaleRule>() }),
                Lifetime.Singleton);
            
            
            //builder.Register<ICustomerSpawner, DefaultCustomerSpawner>(Lifetime.Singleton);
            //builder.Register<ICustomerSpawner, FifteenCustomersSinglePassiveAttemptSpawner>(Lifetime.Singleton); //TEST was created to test zero books selected
            //builder.Register<ICustomerSpawner, ActiveRequestsOnlyCustomerSpawner>(Lifetime.Singleton); //TEST 3-5 active-request-only customers (1 request each)
            //builder.Register<ICustomerSpawner, OneToThreePassiveAttemptsCustomerSpawner>(Lifetime.Singleton); //TEST 1-N passive purchases
            builder.Register<ICustomerSpawner, TenCustomersThreeActiveAfterPassiveSpawner>(Lifetime.Singleton); //TEST 10 customers, 1-2 passive each; first 3 also active after passive
            //builder.Register<ICustomerSpawner, TenCustomersThreeActiveBetweenPassivesSpawner>(Lifetime.Singleton); //TEST 10 customers, 1-2 passive each; first 3: passive -> active -> 1 passive
            
            
            // Tuning comes from a designer-editable SO when assigned; otherwise code defaults.
            builder.RegisterInstance(salesTuningConfig != null ? salesTuningConfig.BuildTuning() : new SalesTuning());
            builder.Register<ISalesShelfBuilder, SalesShelfBuilder>(Lifetime.Singleton);
            // Transactional day commit: applies gold/books/shelf/stats/result + day completion atomically
            // at day end. Deps (resources/inventory/shelf-state/sales-stats/day-progress/save) resolve
            // from the parent (global) scope.
            builder.Register<ISalesDayCommitService, SalesDayCommitService>(Lifetime.Singleton);
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
