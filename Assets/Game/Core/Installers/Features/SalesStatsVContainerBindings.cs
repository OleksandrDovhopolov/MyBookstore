using Game.Conditions.API;
using Game.SalesStats.API;
using Game.SalesStats.Conditions;
using Game.SalesStats.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope) — persistent sold counters
    // (per genre, per location×genre, per day×genre). Written at the single sold-book chokepoint
    // (SalesDayCommitService, ISalesStatsRecorder) and read by unlock/quest conditions.
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
                .As<ISalesStatsRecorder>()
                .As<ISalesStatsBaselineSource>();

            // SalesStats ships its own condition adapters; the condition engine discovers them via the
            // IConditionFactory collection — no engine change needed for new condition types.
            builder.Register<IConditionFactory, SoldGenreConditionFactory>(Lifetime.Singleton);
            builder.Register<IConditionFactory, SoldGenreAtLocationConditionFactory>(Lifetime.Singleton);
            builder.Register<IConditionFactory, SoldGenreInSingleDayConditionFactory>(Lifetime.Singleton);
        }
    }
}
