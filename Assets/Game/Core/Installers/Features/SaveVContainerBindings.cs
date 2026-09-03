using Save;
using Save.Config;
using Save.Identity;
using Save.Sync;
using Save.Storage;
using VContainer;

namespace Game.Bootstrap
{
    public static class SaveVContainerBindings
    {
        public static void RegisterSave(this IContainerBuilder builder)
        {
            builder.Register<PersistentInstallPlayerIdentityProvider>(Lifetime.Singleton)
                   .As<IPlayerIdentityProvider>();

            builder.Register<ISaveBackendConfig, SaveBackendConfig>(Lifetime.Singleton);
            builder.Register<LocalDiskStorage>(_ => new LocalDiskStorage(), Lifetime.Singleton);
            builder.Register<HttpSaveStorage>(Lifetime.Singleton);
            builder.Register<ISaveStorage>(resolver => resolver.Resolve<HttpSaveStorage>(), Lifetime.Singleton);
            builder.Register<SaveSyncBootstrap>(Lifetime.Singleton);

            builder.Register(
                       resolver => new SaveService(
                           resolver.Resolve<HttpSaveStorage>(),
                           resolver.Resolve<LocalDiskStorage>()),
                       Lifetime.Singleton)
                   .As<ISaveService>();
        }
    }
}
