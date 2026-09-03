using Analytics;
using Game.Bootstrap.Analytics;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — analytics must be available globally)
    public static class AnalyticsVContainerBindings
    {
        public static void RegisterGameAnalytics(
            this IContainerBuilder builder,
            AnalyticsConfigSO config = null,
            AnalyticsRoutingConfigSO routingConfig = null,
            AnalyticsMappingConfigSO mappingConfig = null)
        {
            builder.Register<AnalyticsPlayerIdentityAdapter>(Lifetime.Singleton)
                .As<global::Analytics.IPlayerIdentityProvider>();

            global::Analytics.AnalyticsVContainerBindings.RegisterAnalytics(
                builder,
                config,
                routingConfig,
                mappingConfig);

            builder.RegisterEntryPoint<QuestAnalyticsListener>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CharacterAnalyticsListener>(Lifetime.Singleton);
            builder.RegisterEntryPoint<LocationUnlockAnalyticsListener>(Lifetime.Singleton);
            builder.RegisterEntryPoint<DecorAnalyticsListener>(Lifetime.Singleton);
        }
    }
}
