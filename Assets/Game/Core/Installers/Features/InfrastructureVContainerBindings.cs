using Game.Commands;
using Game.Http;
using Infrastructure;
using Infrastructure.Audio;
using Game.Logging;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — shared networking layer).
    // Ports here are consumed by any feature that needs to issue HTTP via Game.Http commands.
    public static class InfrastructureVContainerBindings
    {
        public static void RegisterInfrastructure(this IContainerBuilder builder)
        {
            builder.Register<LoggerSettingsService>(
                _ => new LoggerSettingsService(UnityEngine.Resources.Load<LoggerSettings>("LoggerSettings")),
                Lifetime.Singleton);
            builder.Register<ILoggerSettingsService>(r => r.Resolve<LoggerSettingsService>(), Lifetime.Singleton);
            builder.Register<GameLogger>(r => new GameLogger(r.Resolve<LoggerSettingsService>()), Lifetime.Singleton);
            builder.Register<ILogService>(r => r.Resolve<GameLogger>(), Lifetime.Singleton);

            // Command infrastructure ports.
            builder.Register<ICommandLogger>(r => new CommandLoggerAdapter(r.Resolve<ILogService>()), Lifetime.Singleton);
            builder.Register<ICommandErrorReporter, NoOpCommandErrorReporter>(Lifetime.Singleton);

            // HTTP transport: factory builds IRequest adapters, ConnectionService wraps it
            // with internet-availability checks and retry hooks.
            builder.Register<IRequestFactory, UnityWebRequestFactory>(Lifetime.Singleton);
            builder.Register<IConnectionService, ConnectionService>(Lifetime.Singleton);

            // Addressables: catalog init + remote-catalog update (CDN — Cloudflare R2).
            // ProdAddressablesWrapper — статика, не биндится; потребители вызывают Load/Release напрямую.
            // Прогрев каталога — теперь часть LoadingOrchestrator (AddressablesUpdateOperation).
            builder.Register<IAddressablesCatalogService, AddressablesCatalogService>(Lifetime.Singleton);

            // Audio: infrastructure-level Unity Audio wrapper. Gameplay features depend on IAudioService,
            // not on AudioSource/AudioRoot details.
            builder.Register<IAudioSettingsStore, PlayerPrefsAudioSettingsStore>(Lifetime.Singleton);
            builder.Register<IAudioService, AudioService>(Lifetime.Singleton);

            // TODO: Auth token provider
            // builder.Register<IAuthTokenProvider, JwtAuthTokenProvider>(Lifetime.Singleton);

            // TODO: Remote config loader
            // builder.Register<IRemoteConfigService, RemoteConfigService>(Lifetime.Singleton);

            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Resolve<ILogService>();
                Audio.Bind(resolver.Resolve<IAudioService>());
            });
        }
    }
}
