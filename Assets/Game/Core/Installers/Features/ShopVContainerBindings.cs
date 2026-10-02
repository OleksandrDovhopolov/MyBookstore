using System;
using Game.Conditions.API;
using Game.Shop.API;
using Game.Shop.Conditions;
using Game.Shop.Services;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — shop state must survive scene
    // transitions, and ShopService is an ISaveHook that runs on the boot-time save load).
    // Resolves from the same scope: ISaveService, IResourcesService, IRewardGrantService,
    // IConfigsService.
    public static class ShopVContainerBindings
    {
        public static void RegisterShop(this IContainerBuilder builder)
        {
            builder.Register<SaveBackedShopRepository>(Lifetime.Singleton);
            builder.Register<IShopRewardSpecProvider, ShopConfigRewardSpecProvider>(Lifetime.Singleton);

            // Lazy parser for the per-lot unlock conditions in shop.json. ShopService is built at
            // scope start (the analytics entry point below depends on IShopService), so injecting
            // IConditionParser directly would force the whole IConditionFactory collection - and the
            // save hooks several factories register in their constructors - at that moment, changing
            // the global save-hook order. Same lazy shape as Func<ITutorialService> in
            // TutorialVContainerBindings. ShopService resolves it inside WarmupCatalog, during load.
            builder.Register<Func<IConditionParser>>(
                resolver => () => resolver.Resolve<IConditionParser>(),
                Lifetime.Singleton);

            // ShopService self-registers as ISaveHook in its constructor.
            builder.Register<ShopService>(Lifetime.Singleton)
                .AsImplementedInterfaces()  // IShopService + ISaveHook
                .AsSelf();

            // PR8: analytics listener auto-starts at scope creation and forwards LotPurchased events
            // to IAnalyticsService. Disposed on scope teardown (un-subscribes from the event).
            builder.RegisterEntryPoint<ShopAnalyticsListener>(Lifetime.Singleton);

            // PR9: confirmation policy. UI consumers (NewspaperWindow, future Classic Shop) check
            // the policy before BuyAsync and show a ConfirmDialog when required.
            builder.Register<IShopConfirmationPolicy, ThresholdConfirmationPolicy>(Lifetime.Singleton);

            // "shopPurchases" condition adapter, discovered via the IConditionFactory collection.
            // The Func is registered as its own service (NOT the factory via a Func-registration) because
            // the IConditionFactory collection cannot hold two Func-based entries - WeatherIsConditionFactory
            // is already one. Same shape as Func<ITutorialService> in TutorialVContainerBindings.
            builder.Register<Func<IShopService>>(
                resolver => () => resolver.Resolve<IShopService>(),
                Lifetime.Singleton);
            builder.Register<IConditionFactory, ShopPurchasesConditionFactory>(Lifetime.Singleton);
        }
    }
}
