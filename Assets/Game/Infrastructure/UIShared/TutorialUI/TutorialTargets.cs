using UnityEngine;

namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Thin static facade over <see cref="ITutorialTargetRegistry"/> for scene/prefab MonoBehaviours
    /// (e.g. <see cref="TutorialTargetTag"/>) that cannot receive constructor injection. Bound once at
    /// bootstrap. DI-managed consumers (step handlers) should depend on ITutorialTargetRegistry directly.
    /// Mirrors <c>Infrastructure.Audio.Audio</c>.
    /// </summary>
    public static class TutorialTargets
    {
        private static ITutorialTargetRegistry _registry;

        public static bool IsAvailable => _registry != null;

        public static void Bind(ITutorialTargetRegistry registry)
        {
            _registry = registry;
        }

        public static void Clear(ITutorialTargetRegistry registry = null)
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
