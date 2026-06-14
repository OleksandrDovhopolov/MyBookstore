using Game.Resources.API;
using Game.Resources.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — currencies must be available before
    // FTUE seeds the starter gold and before GameplayScene's Results phase reads/writes them).
    // Resolves from the same scope: ISaveService.
    //
    // GameHudView (debug HUD) lives in GameplayScene and is auto-injected from
    // GameplayLifetimeScope as an AutoInjectedGameObject — it is NOT registered here.
    public static class ResourcesVContainerBindings
    {
        public static void RegisterResources(this IContainerBuilder builder)
        {
            builder.Register<IResourcesRepository, SaveBackedResourcesRepository>(Lifetime.Singleton);

            // ResourcesService self-registers as ISaveHook in its constructor.
            builder.Register<ResourcesService>(Lifetime.Singleton)
                .As<IResourcesService>();
        }
    }
}
