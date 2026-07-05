using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    public interface IResourceAnimationTargetRegistry
    {
        void Register(string targetId, RectTransform target);
        void Unregister(string targetId, RectTransform target);
        bool TryGetTarget(string targetId, out RectTransform target);
    }
}
