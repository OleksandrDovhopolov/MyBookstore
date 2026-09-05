using Analytics;
using Game.Privacy.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — the consent decision gates loading itself,
    // so it must exist before any phase runs).
    //
    // REL-5: consent is stored in PlayerPrefs, not through ISaveService, because ConsentGateOperation runs
    // in phase_technical_init — long before SaveDataLoadOperation in phase_data_load.
    public static class ConsentVContainerBindings
    {
        public static void RegisterConsent(this IContainerBuilder builder, string privacyUrl)
        {
            builder.RegisterInstance(new PrivacyLinkSettings(privacyUrl));
            builder.Register<IConsentStore, PlayerPrefsConsentStore>(Lifetime.Singleton);
            builder.Register<ConsentService>(Lifetime.Singleton)
                .As<IAnalyticsConsentService>()
                .As<IConsentGateService>()
                .AsSelf();
        }
    }
}
