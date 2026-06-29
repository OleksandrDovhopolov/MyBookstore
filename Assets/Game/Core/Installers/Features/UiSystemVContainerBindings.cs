using Game.Bootstrap.Loading;
using Game.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — UI stack must persist across scenes)
    public static class UiSystemVContainerBindings
    {
        public static void RegisterUiSystem(this IContainerBuilder builder, UICanvasRoot uiCanvasRootPrefab)
        {
            // UIManagerCanvas instance is spawned at scene root and calls DontDestroyOnLoad
            // from its own Awake (see UICanvasRoot). UnderTransform(null) prevents VContainer
            // from re-parenting it under the LifetimeScope GameObject.
            builder.RegisterComponentInNewPrefab(uiCanvasRootPrefab, Lifetime.Singleton)
                .UnderTransform((Transform)null)
                .As<IUICanvasRoot>();

            // Real cover/reveal lives on the same UIManagerCanvas prefab (sibling of UICanvasRoot).
            // Resolve it from the spawned prefab instead of registering the NoOp (RegisterGameLoading).
            // Falls back to NoOp (with a warning) until the prefab gets the component, so boot never breaks.
            builder.Register<ITransitionAnimationService>(r =>
            {
                var root = r.Resolve<IUICanvasRoot>() as Component;
                var service = root != null ? root.GetComponent<TransitionAnimationService>() : null;
                if (service != null) return service;

                Debug.LogWarning("[Transition] SpriteTransitionAnimationService is missing on the " +
                                 "UIManagerCanvas prefab — using a no-op cover. Add the component + cover layer.");
                return new NoOpTransitionAnimationService();
            }, Lifetime.Singleton);

            builder.Register<IWindowFactory, AddressablesWindowFactory>(Lifetime.Singleton);
            builder.Register<IUIStorage, UIStorage>(Lifetime.Singleton);
            builder.Register<IUISortingController, UISortingController>(Lifetime.Singleton);
            builder.Register<IUIStack, UIStack>(Lifetime.Singleton);
            builder.Register<IUiFilter, UiFilter>(Lifetime.Singleton);
            builder.Register<LockMonitor>(Lifetime.Singleton);

            builder.Register<UIManager>(Lifetime.Singleton)
                .As<IUIManager>()
                .AsSelf();

            // Force eager instantiation of UIManagerCanvas + UIManager at container build.
            // Without this they stay lazy and the canvas never spawns until the first ShowAsync
            // (which means UiPilotDebugPanel on the canvas root never gets a chance to render).
            builder.RegisterBuildCallback(resolver => resolver.Resolve<IUIManager>());
        }
    }
}
