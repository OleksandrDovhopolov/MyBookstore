using Book.Sell.API;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.DayCycle.Morning.UI;
using Game.DayCycle.Results.Services;
using Game.DayCycle.Results.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Main Feature (core loop). Registered in: GameInstaller (GameplayLifetimeScope).
    // Resolves from parent (GlobalLifetimeScope): ISaveService, IConfigsService, ISceneTransitionService.
    // Hosts the shared day-state (IDayProgressService), the Morning phase, and the Results phase.
    public static class DayCycleVContainerBindings
    {
        public static void RegisterDayCycle(this IContainerBuilder builder)
        {
            // --- Shared day progress (day / phase / gold / reputation) — backbone of the core loop ---
            builder.Register<IDayProgressService, DayProgressService>(Lifetime.Singleton);

            // --- Bridge to Sales: lets Book.Sell read the current day without referencing DayCycle ---
            builder.Register<ICurrentDayProvider, DayProgressCurrentDayProvider>(Lifetime.Singleton);

            // --- Morning phase ---
            builder.Register<IMorningContextResolver, MorningContextResolver>(Lifetime.Singleton);
            builder.Register<IMorningSessionService, MorningSessionService>(Lifetime.Singleton);
            if (Object.FindAnyObjectByType<MorningScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<MorningScreenView>();

            // --- Results phase ---
            builder.Register<IResultsRewardService, DefaultResultsRewardService>(Lifetime.Singleton);
            builder.Register<IResultsReviewTextProvider, DefaultResultsReviewTextProvider>(Lifetime.Singleton);
            builder.Register<IResultsSessionService, ResultsSessionService>(Lifetime.Singleton);
            if (Object.FindAnyObjectByType<ResultsScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<ResultsScreenView>();
        }
    }
}
