using Game.Ftue.Services;
using VContainer;

namespace Game.Bootstrap
{
    // FTUE bootstrapper. Registered in GlobalLifetimeScope via BootstrapInstaller because it has to
    // be available in the boot scene before the GameplayScene transition.
    // Resolves from the same scope: ISaveService, IConfigsService.
    public static class FtueVContainerBindings
    {
        public static void RegisterFtue(this IContainerBuilder builder)
        {
            builder.Register<IFtueBootstrapper, FtueBootstrapper>(Lifetime.Singleton);
        }
    }
}
