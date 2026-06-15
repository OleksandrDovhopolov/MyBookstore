using Game.WorldHud;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — WorldHudManager outlives scene changes
    // because customers and their bubbles live in the gameplay scene; a future fade transition will
    // detach all bubbles cleanly).
    public static class WorldHudVContainerBindings
    {
        public static void RegisterWorldHud(this IContainerBuilder builder)
        {
            builder.Register<IWorldHudFactory, AddressablesWorldHudFactory>(Lifetime.Singleton);

            builder.Register<WorldHudManager>(Lifetime.Singleton)
                .As<IWorldHudManager>()
                .AsSelf();

            // Lazy by design — manager is created on first Attach call.
            // No RegisterBuildCallback here (unlike UI System, which needs the canvas spawned at boot).
        }
    }
}
