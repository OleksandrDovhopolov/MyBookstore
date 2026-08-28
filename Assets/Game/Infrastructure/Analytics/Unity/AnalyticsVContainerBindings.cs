using VContainer;

namespace Analytics
{
    public static class AnalyticsVContainerBindings
    {
        public static void RegisterAnalytics(
            this IContainerBuilder builder,
            AnalyticsConfigSO config = null,
            AnalyticsRoutingConfigSO routingConfig = null,
            AnalyticsMappingConfigSO mappingConfig = null)
        {
            if (config != null)
            {
                builder.RegisterInstance<IAnalyticsConfig>(config);
            }
            else
            {
                builder.Register<IAnalyticsConfig, DefaultAnalyticsConfig>(Lifetime.Singleton);
            }

            if (routingConfig != null)
            {
                builder.RegisterInstance<IAnalyticsRoutingConfig>(routingConfig);
            }
            else
            {
                builder.Register<IAnalyticsRoutingConfig, DefaultAnalyticsRoutingConfig>(Lifetime.Singleton);
            }

            if (mappingConfig != null)
            {
                builder.RegisterInstance<IAnalyticsMappingConfig>(mappingConfig);
            }
            else
            {
                builder.Register<IAnalyticsMappingConfig, DefaultAnalyticsMappingConfig>(Lifetime.Singleton);
            }

            builder.Register<IAnalyticsEventFactory, AnalyticsEventFactory>(Lifetime.Singleton);
            builder.Register<IAnalyticsContextProvider, UnityAnalyticsContextProvider>(Lifetime.Singleton);
            builder.Register<IAnalyticsEventValidator, DefaultAnalyticsEventValidator>(Lifetime.Singleton);
            builder.Register<IAnalyticsRouter, DefaultAnalyticsRouter>(Lifetime.Singleton);
            builder.Register<IAnalyticsEventMapper, DefaultAnalyticsEventMapper>(Lifetime.Singleton);
            builder.Register<IAnalyticsQueue, AnalyticsQueue>(Lifetime.Singleton);
            // Real, persisted consent. This whole method is currently unused (BootstrapInstaller binds
            // NullAnalyticsService instead), but when the composite pipeline is revived it must start out
            // consent-correct rather than with the always-allow stub.
            builder.Register<IConsentStore, PlayerPrefsConsentStore>(Lifetime.Singleton);
            builder.Register<ConsentService>(Lifetime.Singleton)
                .As<IAnalyticsConsentService>()
                .As<IConsentGateService>()
                .AsSelf();

            builder.Register<DebugAnalyticsProvider>(Lifetime.Singleton).As<IAnalyticsProvider>();
            builder.Register<FirebaseAnalyticsProvider>(Lifetime.Singleton).As<IAnalyticsProvider>();

            builder.Register<IAnalyticsService, CompositeAnalyticsService>(Lifetime.Singleton);
            builder.RegisterBuildCallback(resolver => resolver.Resolve<IAnalyticsService>().Initialize());
        }
    }
}
