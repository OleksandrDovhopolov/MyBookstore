using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — analytics must be available globally)
    public static class AnalyticsVContainerBindings
    {
        public static void RegisterAnalytics(this IContainerBuilder builder)
        {
            // TODO: Analytics config (ScriptableObject)
            // builder.RegisterInstance(_analyticsConfig);

            // TODO: Analytics providers (Firebase, GameAnalytics, AppsFlyer, etc.)
            // builder.Register<IFirebaseAnalyticsProvider, FirebaseAnalyticsProvider>(Lifetime.Singleton);
            // builder.Register<IDebugAnalyticsProvider, DebugAnalyticsProvider>(Lifetime.Singleton);

            // TODO: Composite analytics service that fans out to all providers
            // builder.Register<IAnalyticsService, CompositeAnalyticsService>(Lifetime.Singleton)
            //     .AsImplementedInterfaces();

            // TODO: Analytics event router / mapping config
            // builder.RegisterInstance(_analyticsRoutingConfig);
            // builder.Register<IAnalyticsEventRouter, AnalyticsEventRouter>(Lifetime.Singleton);
        }
    }
}
