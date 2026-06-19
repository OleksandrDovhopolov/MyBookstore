using Book.Sell.API;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.DayCycle.Morning.UI;
using Game.DayCycle.Results.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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

            builder.Register<IResultsRewardService, DefaultResultsRewardService>(Lifetime.Singleton);
            builder.Register<IResultsReviewTextProvider, DefaultResultsReviewTextProvider>(Lifetime.Singleton);
            builder.Register<IResultsSessionService, ResultsSessionService>(Lifetime.Singleton);
        }

        public static void RegisterDayCycleSceneViews(this IContainerBuilder builder)
        {
            if (Object.FindAnyObjectByType<MorningScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<MorningScreenView>();
        }
    }
}
