using Game.Conditions.API;
using Game.SalesStats.API;
using Game.SalesStats.Conditions;
using Game.SalesStats.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) — persistent per-genre sold counters.
    // Written during the Sales phase via the SoldBookCommitter врезка (ISalesStatsRecorder, resolved
    // from this parent scope by the location-scoped committer) and read by future unlock conditions.
    // Resolves from the same scope: ISaveService, IConfigsService.
    public static class SalesStatsVContainerBindings
    {
        public static void RegisterSalesStats(this IContainerBuilder builder)
        {
            builder.Register<ISalesStatsRepository, SaveBackedSalesStatsRepository>(Lifetime.Singleton);

            // SalesStatsService self-registers as ISaveHook in its constructor. One instance exposed
            // under all three seams (read / write / full + Changed).
            builder.Register<SalesStatsService>(Lifetime.Singleton)
                .As<ISalesStatsService>()
                .As<ISalesStatsReader>()
                .As<ISalesStatsRecorder>();

            // SalesStats ships its own condition adapter ("soldGenre"); the condition engine discovers
            // it via the IConditionFactory collection — no engine change needed for new condition types.
            builder.Register<IConditionFactory, SoldGenreConditionFactory>(Lifetime.Singleton);
        }
    }
}
