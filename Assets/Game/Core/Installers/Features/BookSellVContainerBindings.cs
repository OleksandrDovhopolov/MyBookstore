using System;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Services.Director;
using Book.Sell.UI;
using Book.Sell.UI.Customer;
using Game.Configs;
using Game.Decor.Services;
using Game.Quest.API;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

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
            // TEMP DEBUG: keep economy/location/decor modifiers, but floor passive sale chance at 50%.
            // Restore EconomyBasedSaleChanceCalculator when sales-flow testing is done.
            builder.Register<IBaseSaleChanceCalculator, DebugMinimumSaleChanceCalculator>(Lifetime.Singleton);
        }

        // Passive sales v2 (requested-genre): each customer rolls one genre from its profile.
        // Both hit and miss carry the chosen genre. Default model.
        public static void RegisterRequestedGenrePassiveSales(this IContainerBuilder builder)
        {
            builder.Register<IPassivePurchaseResolver, RequestedGenrePassiveResolver>(Lifetime.Singleton);
        }

        // Legacy passive (ADR-0004 shelf-roll): kept behind the seam for rollback. Not called by default.
        //TODO delete this
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
            SalesTuningConfig salesTuningConfig = null,
            SalesTrafficConfig salesTrafficConfig = null)
        {
            // Reused pure-domain services.
            builder.Register<ISalesRandom, UnityRandomSalesRandom>(Lifetime.Singleton);
            // ISalesSetupProvider is registered in LocationInstaller (PreparationSalesSetupProvider),
            // which reads the player's choice from preparation.session and falls back to the
            // catalog if no session exists yet.
            builder.Register<IBookConditionRequestEvaluator, BookConditionRequestEvaluator>(Lifetime.Singleton);
            builder.Register<IActiveRequestRuntimeProvider, ConfigActiveRequestRuntimeProvider>(Lifetime.Singleton);
            builder.Register<IActiveRequestScoringService, ActiveRequestScoringService>(Lifetime.Singleton);

            // Passive sale chance gate (ADR-0004) resolves from the global scope so HUD previews and
            // sales use the same calculator instance.
            // Per-customer desire profile — used by the spawner in both passive models.
            builder.Register<IDemandGenreWeightProvider, SalesTuningDemandGenreWeightProvider>(Lifetime.Singleton);
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
            
            

            // Fire-once memory for scripted dialogues (GAME-6). Save-backed; ISaveService resolves from the
            // parent (global) scope. Used by the quest-scheduling spawner (filter) and DialoguePresenter (mark).
            builder.Register<IDeliveredDialoguesService, SaveBackedDeliveredDialoguesService>(Lifetime.Singleton);

            // Customer traffic count (how many regular customers per day). Global knobs come from the
            // SalesTrafficConfig SO (or code defaults); per-day counts live in days.json (DayConfig).
            // Contributors are feature-owned: LocationTrafficContributor here, DecorTrafficContributor in
            // RegisterDecor (global scope). VContainer cannot auto-aggregate a single IReadOnlyList across
            // parent+child scopes, so the resolver composes them explicitly (concrete resolves walk up to
            // the parent). See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
            builder.RegisterInstance(salesTrafficConfig != null ? salesTrafficConfig.BuildSettings() : new SalesTrafficSettings());
            builder.Register<LocationTrafficContributor>(Lifetime.Singleton);
            builder.Register<ICustomerTrafficResolver>(r => new CustomerTrafficResolver(
                    r.Resolve<SalesTrafficSettings>(),
                    r.Resolve<IConfigsService>(),
                    new ICustomerTrafficContributor[]
                    {
                        r.Resolve<LocationTrafficContributor>(),
                        r.Resolve<DecorTrafficContributor>() // registered in RegisterDecor (parent scope)
                    }),
                Lifetime.Singleton);
            // How many customers arrive with an active request. Same shape/knobs as the traffic resolver;
            // the requests.json catalog is only a pool to draw from and must never size the day.
            // Contributor list is empty for now — the seam is here for decor/events to plug into later.
            builder.Register<IActiveRequestCountResolver>(r => new ActiveRequestCountResolver(
                    r.Resolve<SalesTrafficSettings>(),
                    r.Resolve<IConfigsService>(),
                    Array.Empty<IActiveRequestCountContributor>()),
                Lifetime.Singleton);

            // Warns once per process if a day asks for more active requests than it has customers.
            builder.RegisterEntryPoint<CustomerTrafficConfigValidator>(Lifetime.Singleton);

            // Base composition (concrete type) + the quest-replacing decorator as ICustomerSpawner (GAME-6).
            // The decorator replaces regular customer slots with ACTIVE quest dialogue customers instead of
            // increasing the total visitor count. NOTE: register the inner concretely —
            // resolving ICustomerSpawner inside the ICustomerSpawner factory would be a self-reference. Swap
            // the inner type here to change base composition. IQuestsService resolves from the global scope.
            builder.Register<RegularCustomerSpawner>(r => new RegularCustomerSpawner(
                    r.Resolve<IConfigsService>(),
                    r.Resolve<ICustomerTrafficResolver>(),
                    r.Resolve<IActiveRequestRuntimeProvider>(),
                    r.Resolve<ICustomerProfileProvider>(),
                    r.Resolve<IActiveRequestCountResolver>()),
                Lifetime.Singleton); // production base: count from ICustomerTrafficResolver
            builder.Register<ICustomerSpawner>(r => new QuestReplacingCustomerSpawner(
                    r.Resolve<RegularCustomerSpawner>(),
                    r.Resolve<IConfigsService>(),
                    r.Resolve<IQuestsService>(),
                    r.Resolve<IDeliveredDialoguesService>(),
                    r.Resolve<ICustomerProfileProvider>()),
                Lifetime.Singleton);
            
            
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

            // Opens DialogWindow when a scripted dialogue starts (GAME-6 §Этап 5). Same wiring as the
            // minigame presenter: IUIManager from the parent scope, controller via WindowArgs. The window
            // owns completion (CompleteDialogue on end); the presenter is the safety-net if opening fails.
            builder.RegisterEntryPoint<DialoguePresenter>(Lifetime.Singleton);

            // Debug screen. Registered only if present in the scene, so the project runs before the UI
            // is wired. Same pattern as MorningScreenView.
            if (Object.FindAnyObjectByType<SalesScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<SalesScreenView>();
        }
    }
}
