using Game.Bootstrap.Loading;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — survives scene transitions).
    // EntryPoint больше нет: Bootstrap — MonoBehaviour в boot-сцене (см. Bootstrap.cs),
    // автоинжектится через scene-local LifetimeScope как child of GlobalLifetimeScope.prefab.
    public static class GameLoadingVContainerBindings
    {
        public static void RegisterGameLoading(this IContainerBuilder builder)
        {
            builder.Register<LoadingProgressAggregator>(_ => new LoadingProgressAggregator(), Lifetime.Singleton);
            builder.Register<LoadingOrchestrator>(Lifetime.Singleton);

            // Generic-сервис переходов между сценами. Тонкая обёртка над SceneManager,
            // используется SceneTransitionOperation и gameplay-кодом (GameFlowService).
            builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

            // Хук анимации перехода (cover/reveal). Сейчас no-op; реальный fade — отдельной задачей.
            builder.Register<ITransitionAnimationService, NoOpTransitionAnimationService>(Lifetime.Singleton);
        }
    }
}
