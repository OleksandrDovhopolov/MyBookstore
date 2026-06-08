using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — shared networking layer)
    public static class InfrastructureVContainerBindings
    {
        public static void RegisterInfrastructure(this IContainerBuilder builder)
        {
            // TODO: HTTP web client options (base URL, timeout, default headers)
            // builder.RegisterInstance(new WebClientOptions { BaseUrl = ApiConfig.BaseUrl, TimeoutSeconds = 30 });

            // TODO: Auth token provider
            // builder.Register<IAuthTokenProvider, JwtAuthTokenProvider>(Lifetime.Singleton);

            // TODO: HTTP web client
            // builder.Register<IWebClient, UnityWebRequestWebClient>(Lifetime.Singleton);

            // TODO: Authorization service (real or mock)
            // builder.Register<IAuthorizationService, AuthorizationService>(Lifetime.Singleton);

            // TODO: Remote config loader
            // builder.Register<IRemoteConfigService, RemoteConfigService>(Lifetime.Singleton);
        }
    }
}
