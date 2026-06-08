using VContainer;

namespace Game.Bootstrap
{
    // Registered in: GameInstaller (GameplayLifetimeScope)
    // Resolves from parent: IWebClient, IWindowFactory, IAnalyticsService
    // Resolves from scene scope: IResourceManager, IInventoryService, IIapPurchaseService
    public static class ShopVContainerBindings
    {
        public static void RegisterShop(this IContainerBuilder builder)
        {
            // TODO: Shop catalog config (offer definitions, sections — ScriptableObject)
            // builder.RegisterInstance(_shopConfig);

            // TODO: Shop server API
            // builder.Register<IShopServerApi, HttpShopServerApi>(Lifetime.Singleton);

            // TODO: Offer provider — fetches personalized offers from server
            // builder.Register<IShopOfferProvider, ServerShopOfferProvider>(Lifetime.Singleton);

            // TODO: Shop purchase handler — orchestrates IAP or soft-currency purchases
            // builder.Register<IShopPurchaseHandler, ShopPurchaseHandler>(Lifetime.Singleton);

            // TODO: Shop service — public API used by UI and other features
            // builder.Register<IShopService, ShopService>(Lifetime.Singleton);

            // TODO: Shop window router
            // builder.Register<IShopWindowRouter, ShopWindowRouter>(Lifetime.Transient);
        }
    }
}
