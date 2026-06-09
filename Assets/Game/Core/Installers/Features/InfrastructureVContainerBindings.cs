using Game.Commands;
using Game.Http;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — shared networking layer).
    // Ports here are consumed by any feature that needs to issue HTTP via Game.Http commands.
    public static class InfrastructureVContainerBindings
    {
        public static void RegisterInfrastructure(this IContainerBuilder builder)
        {
            // Command infrastructure ports.
            // UnityCommandLogger takes CommandLogLevel — у параметра есть значение по умолчанию,
            // но VContainer всё равно пытается резолвить его как зависимость (No such registration).
            // Поэтому явно через factory-делегат с нужным уровнем.
            builder.Register<ICommandLogger>(_ => new UnityCommandLogger(CommandLogLevel.Info), Lifetime.Singleton);
            builder.Register<ICommandErrorReporter, NoOpCommandErrorReporter>(Lifetime.Singleton);

            // HTTP transport: factory builds IRequest adapters, ConnectionService wraps it
            // with internet-availability checks and retry hooks.
            builder.Register<IRequestFactory, UnityWebRequestFactory>(Lifetime.Singleton);
            builder.Register<IConnectionService, ConnectionService>(Lifetime.Singleton);

            // TODO: Auth token provider
            // builder.Register<IAuthTokenProvider, JwtAuthTokenProvider>(Lifetime.Singleton);

            // TODO: Remote config loader
            // builder.Register<IRemoteConfigService, RemoteConfigService>(Lifetime.Singleton);
        }
    }
}
