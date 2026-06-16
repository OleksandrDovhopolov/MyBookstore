using Game.Shop.API;
using Game.Shop.Services;
using VContainer;

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

            // ShopService self-registers as ISaveHook in its constructor.
            builder.Register<ShopService>(Lifetime.Singleton)
                .AsImplementedInterfaces()  // IShopService + ISaveHook
                .AsSelf();
        }
    }
}
