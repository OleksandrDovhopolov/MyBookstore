using Game.Location.API;
using Game.Location.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope).
    // Keeps Addressables location prefab assets cached across LocationScene unloads.
    public static class LocationPrefabVContainerBindings
    {
        public static void RegisterLocationPrefabs(this IContainerBuilder builder)
        {
            builder.Register<ILocationPrefabProvider, LocationPrefabProvider>(Lifetime.Singleton);
        }
    }
}
