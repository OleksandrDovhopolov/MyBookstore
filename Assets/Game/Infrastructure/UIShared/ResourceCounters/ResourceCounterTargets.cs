namespace UIShared
{
    public static class ResourceCounterTargets
    {
        private static IResourceCounterTargetRegistry _registry;

        public static bool IsAvailable => _registry != null;

        public static void Bind(IResourceCounterTargetRegistry registry)
        {
            _registry = registry;
        }

        public static void Clear(IResourceCounterTargetRegistry registry = null)
        {
            if (registry == null || ReferenceEquals(_registry, registry))
                _registry = null;
        }

        public static void Register(IResourceCounterTarget target)
        {
            _registry?.Register(target);
        }

        public static void Unregister(IResourceCounterTarget target)
        {
            _registry?.Unregister(target);
        }

        public static bool TryGetTarget(string resourceId, out IResourceCounterTarget target)
        {
            if (_registry != null) return _registry.TryGetTarget(resourceId, out target);
            target = null;
            return false;
        }
    }
}
