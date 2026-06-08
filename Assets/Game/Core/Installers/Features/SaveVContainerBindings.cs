using Save;
using Save.Identity;
using Save.Storage;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — сохранения переживают смену сцен)
    public static class SaveVContainerBindings
    {
        public static void RegisterSave(this IContainerBuilder builder)
        {
            // Identity
            builder.Register<PersistentInstallPlayerIdentityProvider>(Lifetime.Singleton)
                   .As<IPlayerIdentityProvider>();

            // Storage — по умолчанию LocalDiskStorage (MVP).
            // Для HTTP: закомментируй LocalDiskStorage и раскомментируй блок ниже.
            builder.Register<LocalDiskStorage>(Lifetime.Singleton)
                   .As<ISaveStorage>();

            // HTTP + offline cache:
            // builder.Register<ISaveBackendConfig, SaveBackendConfig>(Lifetime.Singleton); // TODO: реализовать
            // builder.Register<LocalDiskStorage>(Lifetime.Singleton);  // локальный кэш
            // builder.Register<HttpSaveStorage>(Lifetime.Singleton).As<ISaveStorage>();

            // SaveService — Fix 4: регистрируется как ISaveService.
            // VContainer автоматически вызовет Dispose() при разрушении scope,
            // так как SaveService реализует IDisposable.
            builder.Register<SaveService>(Lifetime.Singleton)
                   .As<ISaveService>();
        }
    }
}
