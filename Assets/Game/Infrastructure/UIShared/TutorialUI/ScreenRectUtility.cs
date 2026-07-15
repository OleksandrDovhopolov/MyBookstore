using UnityEngine;

namespace Infrastructure.TutorialUI
{
    /// <summary>
    /// Converts a target <see cref="RectTransform"/> (living on any canvas) into a rect expressed in the
    /// local space of the tutorial overlay's <see cref="RectTransform"/>. Mirrors the world-corners →
    /// screen → local-point flow used by ResourceAnimationEndpointResolver, and is safe to call per-frame
    /// (targets ride AnimatedShowHidePanel tweens / SafeArea offsets).
    /// </summary>
    public static class ScreenRectUtility
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        public static bool TryGetLocalRect(RectTransform target, RectTransform overlayRoot, out Rect localRect)
        {
            localRect = default;
            if (target == null || overlayRoot == null) return false;

            var targetCam = ResolveCanvasCamera(target);
            var overlayCam = ResolveCanvasCamera(overlayRoot);

            target.GetWorldCorners(Corners);

            var screenMin = new Vector2(float.MaxValue, float.MaxValue);
            var screenMax = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                var sp = RectTransformUtility.WorldToScreenPoint(targetCam, Corners[i]);
                screenMin = Vector2.Min(screenMin, sp);
                screenMax = Vector2.Max(screenMax, sp);
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayRoot, screenMin, overlayCam, out var localMin))
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayRoot, screenMax, overlayCam, out var localMax))
                return false;

            localRect = new Rect(
                localMin.x,
                localMin.y,
                localMax.x - localMin.x,
                localMax.y - localMin.y);
            return true;
        }

        private static Camera ResolveCanvasCamera(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }
    }
}
