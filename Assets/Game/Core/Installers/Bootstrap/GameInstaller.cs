using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // MonoInstaller — assign this component to GameplayLifetimeScope._monoInstallers (HUB scene).
    // Registers hub-scope services. Can resolve global services from the parent scope.
    //
    // BookSell / Sales (customers, shelf, SalesScreenView, customer anchors) больше НЕ здесь —
    // они переехали в LocationInstaller (LocationScene). См. docs/GameFlowLoop.md.
    public sealed class GameInstaller : MonoInstaller
    {
        public override void InstallBindings(IContainerBuilder builder)
        {
            builder.RegisterRewardDrop();
            builder.RegisterIap();
            builder.RegisterQuest();
            // RegisterPreparation() переехал в BootstrapInstaller (Global): Preparation теперь окно
            // PreparationWindow, инжектится глобальным resolver-ом фабрики окон.

            //TODO задача избавиться от FindAnyObjectByType
            // Scene-компоненты хаба: связывают visual-root и фазовую маршрутизацию хаба с DI.
            // Регистрируются только если присутствуют в сцене (как MorningScreenView).
            if (Object.FindAnyObjectByType<HubRootBinder>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<HubRootBinder>();

            //TODO задача избавиться от FindAnyObjectByType
            if (Object.FindAnyObjectByType<Game.DayCycle.Day.HubPhaseRouter>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<Game.DayCycle.Day.HubPhaseRouter>();
        }
    }
}
