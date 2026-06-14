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

            builder.Register<IWindowFactory, AddressablesWindowFactory>(Lifetime.Singleton);
            builder.Register<IUIStorage, UIStorage>(Lifetime.Singleton);
            builder.Register<IUISortingController, UISortingController>(Lifetime.Singleton);
            builder.Register<IUIStack, UIStack>(Lifetime.Singleton);
            builder.Register<IUiFilter, UiFilter>(Lifetime.Singleton);
            builder.Register<LockMonitor>(Lifetime.Singleton);

            builder.Register<UIManager>(Lifetime.Singleton)
                .As<IUIManager>()
                .AsSelf();
        }
    }
}
