using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — save data must persist across scenes)
    public static class SaveVContainerBindings
    {
        public static void RegisterSave(this IContainerBuilder builder)
        {
            // TODO: Player identity provider (device ID / account ID)
            // builder.Register<IPlayerIdentityProvider, PlayerIdentityProvider>(Lifetime.Singleton);

            // TODO: Save storage backend — swap between local disk and HTTP as needed
            // builder.Register<ISaveStorage>(_ => new LocalDiskStorage(), Lifetime.Singleton);
            // builder.Register<ISaveStorage>(_ => new HttpSaveStorage(token, _.Resolve<IPlayerIdentityProvider>()), Lifetime.Singleton);

            // TODO: Save migration service — handles schema version upgrades
            // builder.Register<SaveMigrationService>(Lifetime.Singleton);

            // TODO: Save service — the public API for reading/writing persistent state
            // builder.Register<ISaveService, SaveService>(Lifetime.Singleton);
        }
    }
}
