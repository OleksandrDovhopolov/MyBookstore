using Game.Progression.API;
using Game.Progression.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registered in: BootstrapInstaller (GlobalLifetimeScope — reputation is read by Results in
    // GameplayLifetimeScope and resolved from this parent scope).
    // Resolves from the same scope: ISaveService.
    public static class ProgressionVContainerBindings
    {
        public static void RegisterProgression(this IContainerBuilder builder)
        {
            builder.Register<IProgressionRepository, SaveBackedProgressionRepository>(Lifetime.Singleton);

            // ProgressionService self-registers as ISaveHook in its constructor.
            builder.Register<ProgressionService>(Lifetime.Singleton)
                .As<IProgressionService>();
        }
    }
}
