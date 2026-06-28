using Book.Sell.API;
using Game.Conditions.API;
using Game.DayCycle.Conditions;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.DayCycle.Results.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Core-loop services are global because UI windows are created by the global UIManager.
    // Scene MonoBehaviours stay registered by GameplayLifetimeScope.
    public static class DayCycleVContainerBindings
    {
        public static void RegisterDayCycleServices(this IContainerBuilder builder)
        {
            builder.Register<IDayProgressService, DayProgressService>(Lifetime.Singleton);
            builder.Register<ICurrentDayProvider, DayProgressCurrentDayProvider>(Lifetime.Singleton);

            builder.Register<IMorningContextResolver, MorningContextResolver>(Lifetime.Singleton);
            builder.Register<IMorningSessionService, MorningSessionService>(Lifetime.Singleton);

            // Current-day weather read seam + "weatherIs" condition adapter (discovered via IConditionFactory).
            builder.Register<ICurrentDayWeatherProvider, CurrentDayWeatherProvider>(Lifetime.Singleton);
            builder.Register<IConditionFactory, WeatherIsConditionFactory>(Lifetime.Singleton);

            builder.Register<IResultsReviewTextProvider, DefaultResultsReviewTextProvider>(Lifetime.Singleton);
            builder.Register<IResultsSummaryBuilder, ResultsSummaryBuilder>(Lifetime.Singleton);
            builder.Register<IResultsSessionService, ResultsSummarySessionService>(Lifetime.Singleton);
        }
    }
}
