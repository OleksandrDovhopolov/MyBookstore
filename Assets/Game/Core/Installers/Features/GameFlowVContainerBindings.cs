using Game.Bootstrap.Loading;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — survives scene transitions).
    // GameFlowService оркестрирует цикл хаб ↔ локация (см. docs/GameFlowLoop.md).
    // Зависит от ISceneTransitionService + ITransitionAnimationService (RegisterGameLoading).
    public static class GameFlowVContainerBindings
    {
        public static void RegisterGameFlow(this IContainerBuilder builder, GameFlowSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning("[GameFlow] GameFlowSettings is not assigned on BootstrapInstaller — " +
                                 "using code defaults (GameplayScene / LocationScene).");
                settings = ScriptableObject.CreateInstance<GameFlowSettings>();
            }

            builder.RegisterInstance(settings);
            builder.Register<IGameFlowService, GameFlowService>(Lifetime.Singleton);
        }
    }
}
