using Game.Conditions.API;
using Game.LocationVisits.API;
using Game.LocationVisits.Conditions;
using Game.LocationVisits.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) — persistent per-location visit counts
    // plus the runtime current location. Written on successful location entry (PreparationWindow) and
    // cleared on return to hub (GameFlowService). Read by the "visitLocation" / "locationIs" conditions.
    // Resolves from the same scope: ISaveService.
    public static class LocationVisitsVContainerBindings
    {
        public static void RegisterLocationVisits(this IContainerBuilder builder)
        {
            builder.Register<ILocationVisitsRepository, SaveBackedLocationVisitsRepository>(Lifetime.Singleton);

            // LocationVisitService self-registers as ISaveHook in its constructor. One instance exposed
            // under the read / current-location / write seams.
            builder.Register<LocationVisitService>(Lifetime.Singleton)
                .As<ILocationVisitsReader>()
                .As<ICurrentLocationProvider>()
                .As<ILocationVisitChangeSource>()
                .As<ILocationVisitService>();

            // Condition adapters discovered by the engine via the IConditionFactory collection.
            builder.Register<IConditionFactory, VisitLocationConditionFactory>(Lifetime.Singleton);
            builder.Register<IConditionFactory, LocationIsConditionFactory>(Lifetime.Singleton);
        }
    }
}
