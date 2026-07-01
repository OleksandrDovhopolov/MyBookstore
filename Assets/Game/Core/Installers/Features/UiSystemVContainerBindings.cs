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
            // UIManagerCanvas instance is spawned at scene root and survives scene transitions.
            // UnderTransform(null) prevents VContainer from re-parenting it under the LifetimeScope GameObject.
            builder.RegisterComponentInNewPrefab(uiCanvasRootPrefab, Lifetime.Singleton)
                .UnderTransform((Transform)null)
                .As<IUICanvasRoot>()
                .AsSelf();

            // Real cover/reveal is assigned on UICanvasRoot (UIManagerCanvas prefab). The transition router is
            // populated in the build callback after UIManagerCanvas is instantiated, so PlayCover/PlayReveal
            // never resolve IUICanvasRoot at runtime (avoids VContainer Lazy self-reference).
            builder.Register<DeferredTransitionAnimationService>(Lifetime.Singleton)
                .As<ITransitionAnimationService>()
                .AsSelf();

            builder.Register<IWindowFactory, AddressablesWindowFactory>(Lifetime.Singleton);
            builder.Register<IUIStorage, UIStorage>(Lifetime.Singleton);
            builder.Register<IUISortingController, UISortingController>(Lifetime.Singleton);
            builder.Register<IUIStack, UIStack>(Lifetime.Singleton);
            builder.Register<IUiFilter, UiFilter>(Lifetime.Singleton);
            builder.Register<LockMonitor>(Lifetime.Singleton);

            builder.Register<UIManager>(Lifetime.Singleton)
                .As<IUIManager>()
                .AsSelf();

            // Force eager instantiation of UIManagerCanvas at container build. Do not resolve IUIManager here:
            // constructing it pulls the whole UI graph while the canvas singleton may still be initializing.
            builder.RegisterBuildCallback(resolver =>
            {
                var root = resolver.Resolve<IUICanvasRoot>();
                var transition = resolver.Resolve<DeferredTransitionAnimationService>();
                transition.SetTransition(root?.TransitionAnimation);
            });
        }
    }
}
