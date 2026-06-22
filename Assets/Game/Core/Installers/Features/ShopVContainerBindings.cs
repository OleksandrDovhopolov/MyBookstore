using Game.Shop.API;
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
        }
    }
}
