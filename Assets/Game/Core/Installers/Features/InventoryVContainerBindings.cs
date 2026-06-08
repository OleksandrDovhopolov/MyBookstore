using VContainer;

namespace Game.Bootstrap
{
    // Registered in: GameInstaller (GameplayLifetimeScope)
    // Resolves from parent: IWebClient, ISaveService, IAnalyticsService
    public static class InventoryVContainerBindings
    {
        public static void RegisterInventory(this IContainerBuilder builder)
        {
            // TODO: Item category registry (maps item type strings to domain types)
            // builder.Register<IItemCategoryRegistry, ItemCategoryRegistry>(Lifetime.Singleton);

            // TODO: Inventory server API
            // builder.Register<IInventoryServerApi, HttpInventoryServerApi>(Lifetime.Singleton);

            // TODO: Inventory service — add/remove/query items
            // builder.Register<IInventoryService, InventoryModuleService>(Lifetime.Singleton)
            //     .AsImplementedInterfaces();

            // TODO: Inventory view model / UI adapter
            // builder.Register<InventoryViewModel>(Lifetime.Singleton);
        }
    }
}
