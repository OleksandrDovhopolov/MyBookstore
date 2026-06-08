using VContainer;

namespace Game.Bootstrap
{
    // Registered in: GameInstaller (GameplayLifetimeScope)
    // Resolves from parent: IWebClient, ISaveService
    public static class ResourcesVContainerBindings
    {
        public static void RegisterResources(this IContainerBuilder builder)
        {
            // TODO: Resource specs config (currency definitions, resource types — ScriptableObject)
            // builder.RegisterInstance(_resourceSpecsConfig);

            // TODO: Resource server API
            // builder.Register<IResourceServerApi, HttpResourceServerApi>(Lifetime.Singleton);

            // TODO: Resource operations service (add/subtract resources)
            // builder.Register<IResourceOperationsService, ResourceOperationsService>(Lifetime.Singleton);

            // TODO: Resource manager — entry point, syncs resources on startup
            // builder.RegisterEntryPoint<ResourceManager>().As<IResourceManager>();

            // TODO: Currency animation component (scene MonoBehaviour)
            // builder.RegisterComponentInHierarchy<AnimateCurrency>();
        }
    }
}
