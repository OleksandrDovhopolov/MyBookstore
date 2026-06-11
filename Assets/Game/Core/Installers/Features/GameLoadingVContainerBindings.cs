using Game.Bootstrap.Loading;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — survives scene transitions).
    // Заменил три прежних entry point (Addressables/Configs warmup + BookDuneProbe)
    // на единый Phase/Group/Operation-флоу через LoadingOrchestrator.
    public static class GameLoadingVContainerBindings
    {
        public static void RegisterGameLoading(this IContainerBuilder builder)
        {
            builder.Register<LoadingProgressAggregator>(_ => new LoadingProgressAggregator(), Lifetime.Singleton);
            builder.Register<LoadingOrchestrator>(Lifetime.Singleton);

            // Generic-сервис переходов между сценами. Тонкая обёртка над SceneManager,
            // вызывается операцией SceneTransitionOperation и (в будущем) gameplay-кодом.
            builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

            // LoadingScreenView лежит в boot-сцене и НЕ резолвится через DI:
            // при VContainerSettings.RootLifetimeScope=prefab GlobalLifetimeScope инстансится
            // до загрузки boot-сцены, поэтому RegisterComponentInHierarchy ничего бы не нашёл.
            // EntryPoint сам ищет view через FindAnyObjectByType в StartAsync (см. там же).

            builder.RegisterEntryPoint<LoadingOrchestratorEntryPoint>();
        }
    }
}
