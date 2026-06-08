using VContainer;

namespace Game.Bootstrap
{
    // Main feature. Registered in: GameInstaller (GameplayLifetimeScope)
    // Resolves from parent: IWebClient, ISaveService, IAnalyticsService, IInventoryService
    public static class BookSellVContainerBindings
    {
        public static void RegisterBookSell(this IContainerBuilder builder)
        {
            // --- Core ---

            // TODO: Book catalog config (book definitions, genres, prices — ScriptableObject)
            // builder.RegisterInstance(_bookCatalogConfig);

            // TODO: Book catalog provider — fetches/caches book list from server
            // builder.Register<IBookCatalogProvider, RemoteBookCatalogProvider>(Lifetime.Singleton);

            // TODO: Book purchase service — validates, processes and records book purchases
            // builder.Register<IBookPurchaseService, BookPurchaseService>(Lifetime.Singleton);

            // TODO: Book server API
            // builder.Register<IBookServerApi, HttpBookServerApi>(Lifetime.Singleton);

            // --- Presentation ---

            // TODO: Book list UI view model
            // builder.Register<BookListViewModel>(Lifetime.Singleton);

            // TODO: Book detail UI view model
            // builder.Register<BookDetailViewModel>(Lifetime.Transient);

            // TODO: Window router for book sell flow
            // builder.Register<IBookSellWindowRouter, BookSellWindowRouter>(Lifetime.Transient);

            // --- Integration ---

            // TODO: Register with event system / quest tracker when a book is sold
            // builder.RegisterBuildCallback(resolver =>
            // {
            //     var eventRegistry = resolver.Resolve<IEventRegistry>();
            //     eventRegistry.Register<BookSoldEvent>(resolver.Resolve<IQuestService>().HandleBookSold);
            // });
        }
    }
}
