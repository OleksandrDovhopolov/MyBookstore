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

            // Префаб LoadingScreen лежит в boot-сцене (см. иерархию).
            // RegisterComponentInHierarchy ищет компонент в той же сцене, где GlobalLifetimeScope.
            // Если экран будет переезжать в DontDestroyOnLoad — переключим на RegisterComponentInNewPrefab.
            builder.RegisterComponentInHierarchy<LoadingScreenView>();

            builder.RegisterEntryPoint<LoadingOrchestratorEntryPoint>();
        }
    }
}
