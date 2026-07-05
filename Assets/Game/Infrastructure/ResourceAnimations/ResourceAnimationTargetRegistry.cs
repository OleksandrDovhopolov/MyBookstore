using System;
using System.Collections.Generic;
using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    public sealed class ResourceAnimationTargetRegistry : IResourceAnimationTargetRegistry
    {
        private readonly Dictionary<string, RectTransform> _targets = new(StringComparer.Ordinal);

        public void Register(string targetId, RectTransform target)
        {
            if (string.IsNullOrWhiteSpace(targetId) || target == null) return;
            _targets[targetId] = target;
        }

        public void Unregister(string targetId, RectTransform target)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return;
            if (!_targets.TryGetValue(targetId, out var current)) return;
            if (current != target) return;
            _targets.Remove(targetId);
        }

        public bool TryGetTarget(string targetId, out RectTransform target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(targetId)) return false;
            if (!_targets.TryGetValue(targetId, out var current)) return false;
            if (current == null)
            {
                _targets.Remove(targetId);
                return false;
            }

            target = current;
            return true;
        }
    }
}
