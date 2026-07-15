using UnityEngine;

namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Maps a string id to a UI <see cref="RectTransform"/> so the tutorial overlay can highlight it by id.
    /// DI singleton; rects are registered by the DI-managed controllers that own them (e.g. the HUD
    /// controller registers its Start-Day button), mirroring IResourceAnimationTargetRegistry.
    /// </summary>
    public interface ITutorialTargetRegistry
    {
        void Register(string targetId, RectTransform target);
        void Unregister(string targetId, RectTransform target);
        bool TryGetTarget(string targetId, out RectTransform target);
    }
}
