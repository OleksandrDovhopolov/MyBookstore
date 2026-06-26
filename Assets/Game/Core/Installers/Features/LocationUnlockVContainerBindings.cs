using Game.LocationUnlock.API;
using Game.LocationUnlock.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope). Domain layer over the Conditions
    // engine. Resolves from the same scope: ISaveService, IConfigsService, IConditionParser,
    // IResourcesService, ISalesStatsService. Must be registered after RegisterConditions /
    // RegisterSalesStats / RegisterResources (registration order is irrelevant to resolution).
    public static class LocationUnlockVContainerBindings
    {
        public static void RegisterLocationUnlock(this IContainerBuilder builder)
        {
            builder.Register<ILocationUnlockRepository, SaveBackedLocationUnlockRepository>(Lifetime.Singleton);

            // LocationUnlockService self-registers as ISaveHook in its constructor.
            builder.Register<LocationUnlockService>(Lifetime.Singleton)
                .As<ILocationUnlockService>();
        }
    }
}
