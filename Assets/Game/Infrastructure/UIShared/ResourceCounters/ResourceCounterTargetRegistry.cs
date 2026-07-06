using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UIShared
{
    public sealed class ResourceCounterTargetRegistry : IResourceCounterTargetRegistry
    {
        private readonly Dictionary<string, IResourceCounterTarget> _targets = new(StringComparer.Ordinal);

        public event Action<IResourceCounterTarget> TargetRegistered;

        public void Register(IResourceCounterTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ResourceId)) return;

            _targets[target.ResourceId] = target;
            TargetRegistered?.Invoke(target);
        }

        public void Unregister(IResourceCounterTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ResourceId)) return;
            if (!_targets.TryGetValue(target.ResourceId, out var current)) return;
            if (!ReferenceEquals(current, target)) return;

            _targets.Remove(target.ResourceId);
        }

        public bool TryGetTarget(string resourceId, out IResourceCounterTarget target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(resourceId)) return false;
            if (!_targets.TryGetValue(resourceId, out var current)) return false;
            if (current == null || current is Object unityObject && unityObject == null)
            {
                _targets.Remove(resourceId);
                return false;
            }

            target = current;
            return true;
        }
    }
}
