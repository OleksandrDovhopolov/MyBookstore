using Game.Localization;
using VContainer;

namespace Game.Bootstrap
{
    public static class LocalizationVContainerBindings
    {
        public static void RegisterLocalization(this IContainerBuilder builder)
        {
            builder.Register<LocalizationService>(Lifetime.Singleton)
                .As<ILocalizationService>()
                .AsSelf();

            builder.RegisterBuildCallback(resolver =>
                LocalizationLocator.SetService(resolver.Resolve<ILocalizationService>()));
        }
    }
}
