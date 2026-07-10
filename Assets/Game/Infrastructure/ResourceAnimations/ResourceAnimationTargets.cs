using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    /// <summary>
    /// Thin static facade over <see cref="IResourceAnimationTargetRegistry"/> for scene/prefab
    /// MonoBehaviours that cannot receive constructor injection. DI-managed consumers should depend on
    /// <see cref="IResourceAnimationTargetRegistry"/> directly.
    /// </summary>
    public static class ResourceAnimationTargets
    {
        private static IResourceAnimationTargetRegistry _registry;

        public static bool IsAvailable => _registry != null;

        public static void Bind(IResourceAnimationTargetRegistry registry)
        {
            _registry = registry;
        }

        public static void Clear(IResourceAnimationTargetRegistry registry = null)
        {
            if (registry == null || ReferenceEquals(_registry, registry))
                _registry = null;
        }

        public static void Register(string targetId, RectTransform target)
        {
            _registry?.Register(targetId, target);
        }

        public static void Unregister(string targetId, RectTransform target)
        {
            _registry?.Unregister(targetId, target);
        }

        public static bool TryGetTarget(string targetId, out RectTransform target)
        {
            if (_registry != null) return _registry.TryGetTarget(targetId, out target);
            target = null;
            return false;
        }
    }
}
