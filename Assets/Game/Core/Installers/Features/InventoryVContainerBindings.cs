using Game.Inventory.API;
using Game.Inventory.Services;
using Game.Inventory.UI;
using Game.Inventory.UseHandlers;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — inventory must be available before
    // FTUE writes the starter preset and before GameplayScene asks for owned books).
    // Resolves from the same scope: ISaveService, IConfigsService.
    public static class InventoryVContainerBindings
    {
        public static void RegisterInventory(this IContainerBuilder builder)
        {
            // Category registry — single mutable instance seeded at registration time.
            builder.Register<IItemCategoryRegistry>(_ =>
            {
                var registry = new ItemCategoryRegistry();
                registry.Register(new ItemCategory(InventoryCategories.Book,        ItemStackingMode.Unique, "Books"));
                registry.Register(new ItemCategory(InventoryCategories.Decor,       ItemStackingMode.Unique, "Decor"));
                registry.Register(new ItemCategory(InventoryCategories.PuzzlePiece, ItemStackingMode.Stack,  "Puzzle Pieces"));
                return registry;
            }, Lifetime.Singleton);

            builder.Register<IInventoryRepository, SaveBackedInventoryRepository>(Lifetime.Singleton);

            // InventoryService self-registers as ISaveHook in its constructor.
            builder.Register<InventoryService>(Lifetime.Singleton)
                .As<IInventoryService>();
            
            // Use handlers — discovered by InventoryUseRouter via IReadOnlyList<IInventoryItemUseHandler>.
            // Decor activation handler is registered by Game.Decor (RegisterDecor).
            builder.Register<IInventoryItemUseHandler, PuzzleAssembleUseHandler>(Lifetime.Singleton);

            builder.Register<IInventoryUseRouter, InventoryUseRouter>(Lifetime.Singleton);

            /*// Debug UI is registered only when present in the scene (same guard as other feature views).
            if (Object.FindAnyObjectByType<InventoryScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<InventoryScreenView>();*/
        }
    }
}
