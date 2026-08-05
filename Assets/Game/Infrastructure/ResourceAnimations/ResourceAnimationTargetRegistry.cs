using System;
using System.Collections.Generic;
using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    /// <summary>
    /// Flight destinations per target id, kept as a stack rather than a single slot. A window can
    /// register its own counter for a resource the HUD already shows; coins then fly to the window's
    /// counter, and unregistering it restores the HUD one instead of leaving the id with no target.
    /// </summary>
    public sealed class ResourceAnimationTargetRegistry : IResourceAnimationTargetRegistry
    {
        private readonly Dictionary<string, List<RectTransform>> _targets = new(StringComparer.Ordinal);

        public void Register(string targetId, RectTransform target)
        {
            if (string.IsNullOrWhiteSpace(targetId) || target == null) return;

            if (!_targets.TryGetValue(targetId, out var targets))
            {
                targets = new List<RectTransform>(1);
                _targets[targetId] = targets;
            }

            // Re-registering moves the target back on top rather than duplicating it.
            targets.Remove(target);
            Prune(targets);
            targets.Add(target);
        }

        public void Unregister(string targetId, RectTransform target)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return;
            if (!_targets.TryGetValue(targetId, out var targets)) return;

            targets.Remove(target);
            Prune(targets);

            if (targets.Count == 0)
                _targets.Remove(targetId);
        }

        public bool TryGetTarget(string targetId, out RectTransform target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(targetId)) return false;
            if (!_targets.TryGetValue(targetId, out var targets)) return false;

            Prune(targets);
            if (targets.Count == 0)
            {
                _targets.Remove(targetId);
                return false;
            }

            target = targets[targets.Count - 1];
            return true;
        }

        private static void Prune(List<RectTransform> targets)
        {
            for (var i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i] != null) continue;
                targets.RemoveAt(i);
            }
        }
    }
}
