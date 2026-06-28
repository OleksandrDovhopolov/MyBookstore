using Game.LocationEntry.API;
using Game.LocationEntry.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope). Pure read-side calculator for the
    // per-visit entry fee (location base cost + active decor VisitCostDelta). Resolves from the same
    // scope: IConfigsService, IDecorPlacementService. No save state of its own.
    public static class LocationEntryVContainerBindings
    {
        public static void RegisterLocationEntry(this IContainerBuilder builder)
        {
            builder.Register<ILocationEntryCostCalculator, LocationEntryCostCalculator>(Lifetime.Singleton);
        }
    }
}
