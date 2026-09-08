using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Infrastructure.Audio;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public static class MusicDirectorVContainerBindings
    {
        public static void RegisterMusicDirector(this IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<MusicDirector>(
                resolver => new MusicDirector(
                    resolver.Resolve<IAudioService>(),
                    resolver.Resolve<IGameFlowService>(),
                    resolver.Resolve<IDayProgressService>(),
                    resolver.ResolveOrDefault<AudioCatalog>()),
                Lifetime.Singleton);
        }
    }
}
