using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UIShared
{
    /// <summary>
    /// Targets are kept per resource as a stack, not a single slot: a window can bring its own counter
    /// (the shop showing the gold balance) without evicting the HUD one. The most recently registered
    /// live target is the active one — coins fly there — while amount updates go to every live target,
    /// so a counter sitting behind a window is already correct by the time it is visible again.
    /// </summary>
    public sealed class ResourceCounterTargetRegistry : IResourceCounterTargetRegistry
    {
        private static readonly IResourceCounterTarget[] Empty = Array.Empty<IResourceCounterTarget>();

        private readonly Dictionary<string, List<IResourceCounterTarget>> _targets =
            new(StringComparer.Ordinal);

        public event Action<IResourceCounterTarget> TargetRegistered;

        public void Register(IResourceCounterTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ResourceId)) return;

            if (!_targets.TryGetValue(target.ResourceId, out var targets))
            {
                targets = new List<IResourceCounterTarget>(1);
                _targets[target.ResourceId] = targets;
            }

            // Re-registering an existing target moves it back on top rather than duplicating it.
            targets.Remove(target);
            Prune(targets);
            targets.Add(target);

            TargetRegistered?.Invoke(target);
        }

        public void Unregister(IResourceCounterTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ResourceId)) return;
            if (!_targets.TryGetValue(target.ResourceId, out var targets)) return;

            targets.Remove(target);
            Prune(targets);

            if (targets.Count == 0)
                _targets.Remove(target.ResourceId);
        }

        public bool TryGetTarget(string resourceId, out IResourceCounterTarget target)
        {
            target = null;

            var targets = ResolveLiveTargets(resourceId);
            if (targets.Count == 0) return false;

            target = targets[targets.Count - 1];
            return true;
        }

        public IReadOnlyList<IResourceCounterTarget> GetTargets(string resourceId)
            => ResolveLiveTargets(resourceId);

        private IReadOnlyList<IResourceCounterTarget> ResolveLiveTargets(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return Empty;
            if (!_targets.TryGetValue(resourceId, out var targets)) return Empty;

            Prune(targets);
            if (targets.Count != 0) return targets;

            _targets.Remove(resourceId);
            return Empty;
        }

        private static void Prune(List<IResourceCounterTarget> targets)
        {
            for (var i = targets.Count - 1; i >= 0; i--)
            {
                if (IsAlive(targets[i])) continue;
                targets.RemoveAt(i);
            }
        }

        // Destroyed MonoBehaviour targets compare equal to null only through the Unity operator.
        private static bool IsAlive(IResourceCounterTarget target)
        {
            if (target == null) return false;
            return target is not Object unityObject || unityObject != null;
        }
    }
}
