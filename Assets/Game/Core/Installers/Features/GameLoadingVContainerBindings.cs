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

            builder.RegisterEntryPoint<LoadingOrchestratorEntryPoint>();

            // TODO: LoadingScreenView prefab — забиндить после сборки префаба
            // (DontDestroyOnLoad), затем подключить к LoadingOrchestrator.ProgressChanged
            // в LoadingOrchestratorEntryPoint (сейчас прогресс уходит в Debug.Log).
        }
    }
}
