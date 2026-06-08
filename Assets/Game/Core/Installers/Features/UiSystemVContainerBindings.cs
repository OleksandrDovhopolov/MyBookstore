using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — UI stack must persist across scenes)
    public static class UiSystemVContainerBindings
    {
        public static void RegisterUiSystem(this IContainerBuilder builder)
        {
            // TODO: UIManager root prefab (instantiated once, DontDestroyOnLoad)
            // builder.RegisterComponentInNewPrefab(_uiManagerPrefab, Lifetime.Singleton).DontDestroyOnLoad();

            // TODO: Window factory — resolves and instantiates UI windows via DI
            // builder.Register<IWindowFactory, WindowFactoryDI>(Lifetime.Singleton);

            // TODO: Transition animation service
            // builder.Register<ITransitionAnimationService>(resolver =>
            // {
            //     var uiManager = resolver.Resolve<UIManager>();
            //     return uiManager.GetComponent<TransitionAnimationService>();
            // }, Lifetime.Singleton);

            // TODO: Window router / navigation stack
            // builder.Register<IWindowRouter, WindowRouter>(Lifetime.Singleton);

            // NOTE: WindowFactoryDI.SetResolver(resolver) must be called in OnBuildCallback
            // on GlobalLifetimeScope after the container is built.
        }
    }
}
