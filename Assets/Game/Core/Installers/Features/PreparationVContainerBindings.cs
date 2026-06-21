using Game.Preparation.Services;
using Game.Preparation.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Main Feature (core loop) — Preparation phase. Registered in: GameInstaller (GameplayLifetimeScope, HUB).
    // Resolves from parent (GlobalLifetimeScope): ISaveService, IConfigsService.
    // Resolves from sibling fiches: IDayProgressService (DayCycle).
    //
    // ISalesSetupProvider (PreparationSalesSetupProvider) теперь регистрируется в LocationInstaller
    // (location-скоп), т.к. его единственный потребитель — SalesDayController (BookSell), который
    // переехал в LocationScene. Provider читает выбор игрока из save-модуля preparation.session,
    // поэтому переживает границу скопов. См. docs/GameFlowLoop.md.
    public static class PreparationVContainerBindings
    {
        public static void RegisterPreparation(this IContainerBuilder builder)
        {
            builder.Register<IPreparationInventoryProvider, DayProgressInventoryProvider>(Lifetime.Singleton);
            builder.Register<IPreparationSessionService, PreparationSessionService>(Lifetime.Singleton);

            if (Object.FindAnyObjectByType<PreparationScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<PreparationScreenView>();
        }
    }
}
