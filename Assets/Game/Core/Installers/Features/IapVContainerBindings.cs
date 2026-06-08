using VContainer;

namespace Game.Bootstrap
{
    // Registered in: GameInstaller (GameplayLifetimeScope)
    // Resolves from parent: IWebClient, IPlayerIdentityProvider
    public static class IapVContainerBindings
    {
        public static void RegisterIap(this IContainerBuilder builder)
        {
            // TODO: IAP config (products, prices, store IDs — ScriptableObject)
            // builder.RegisterInstance(_iapConfig);

            // TODO: Platform store adapter (Unity IAP / custom)
            // builder.Register<IStoreAdapter, UnityIapStoreAdapter>(Lifetime.Singleton);

            // TODO: IAP receipt validator (server-side)
            // builder.Register<IReceiptValidator, ServerReceiptValidator>(Lifetime.Singleton);

            // TODO: IAP purchase service
            // builder.Register<IIapPurchaseService, IapPurchaseService>(Lifetime.Singleton);

            // TODO: IAP server API
            // builder.Register<IIapServerApi, HttpIapServerApi>(Lifetime.Singleton);
        }
    }
}
