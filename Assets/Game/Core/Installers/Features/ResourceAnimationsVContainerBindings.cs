using Game.UI.ResourceAnimations;
using Infrastructure.ResourceAnimations;
using VContainer;

namespace Game.Bootstrap
{
    public static class ResourceAnimationsVContainerBindings
    {
        public static void RegisterResourceAnimations(
            this IContainerBuilder builder,
            ResourceAnimationSettings settings)
        {
            builder.RegisterInstance(settings != null ? settings : ResourceAnimationSettings.CreateDefault());
            builder.Register<IResourceAnimationTargetRegistry, ResourceAnimationTargetRegistry>(Lifetime.Singleton);
            builder.RegisterBuildCallback(
                resolver => ResourceAnimationTargets.Bind(resolver.Resolve<IResourceAnimationTargetRegistry>()));
            builder.Register<IResourceAnimationService, ResourceAnimationService>(Lifetime.Singleton);
        }
    }
}
