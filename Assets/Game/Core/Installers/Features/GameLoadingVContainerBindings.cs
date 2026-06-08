using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — survives scene transitions)
    public static class GameLoadingVContainerBindings
    {
        public static void RegisterGameLoading(this IContainerBuilder builder)
        {
            // TODO: Loading progress tracking
            // builder.Register<LoadingProgressAggregator>(_ => new LoadingProgressAggregator(), Lifetime.Singleton);

            // TODO: Loading orchestrator — coordinates loading steps across systems
            // builder.Register<LoadingOrchestrator>(Lifetime.Singleton);

            // TODO: Scene transition service
            // builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

            // TODO: Splash / loading screen controller
            // builder.RegisterComponentInNewPrefab(_loadingScreenPrefab, Lifetime.Singleton).DontDestroyOnLoad();
        }
    }
}
