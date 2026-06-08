using Save;
using Save.Config;
using Save.Identity;
using Save.Storage;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — сохранения переживают смену сцен).
    // Depends on InfrastructureVContainerBindings for ICommandLogger / ICommandErrorReporter /
    // IConnectionService / IRequestFactory when HTTP mode is enabled.
    public static class SaveVContainerBindings
    {
        public static void RegisterSave(this IContainerBuilder builder)
        {
            // Identity
            builder.Register<PersistentInstallPlayerIdentityProvider>(Lifetime.Singleton)
                   .As<IPlayerIdentityProvider>();

            // Storage — по умолчанию LocalDiskStorage (MVP).
            // Для HTTP-режима: закомментируй блок LocalDiskStorage и раскомментируй блок HTTP ниже.
            builder.Register<LocalDiskStorage>(Lifetime.Singleton)
                   .As<ISaveStorage>();

            // HTTP + write-through кэш:
            //   HttpSaveStorage конструируется контейнером — все его зависимости
            //   (ISaveBackendConfig, ISaveStorage local cache, IPlayerIdentityProvider,
            //    IConnectionService, ICommandLogger, ICommandErrorReporter) резолвятся автоматически.
            //
            // builder.Register<ISaveBackendConfig, SaveBackendConfig>(Lifetime.Singleton);
            // builder.Register<LocalDiskStorage>(Lifetime.Singleton);  // local write-through cache
            // builder.Register<HttpSaveStorage>(Lifetime.Singleton).As<ISaveStorage>();

            // SaveService — регистрируется как ISaveService.
            // VContainer автоматически вызовет Dispose() при разрушении scope,
            // так как SaveService реализует IDisposable.
            builder.Register<SaveService>(Lifetime.Singleton)
                   .As<ISaveService>();
        }
    }
}
