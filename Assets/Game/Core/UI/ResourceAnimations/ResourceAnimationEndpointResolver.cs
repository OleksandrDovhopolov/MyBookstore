using Infrastructure.ResourceAnimations;
using UnityEngine;

namespace Game.UI.ResourceAnimations
{
    public static class ResourceAnimationEndpointResolver
    {
        public static bool TryResolve(
            ResourceAnimationEndpoint endpoint,
            RectTransform animationRoot,
            IResourceAnimationTargetRegistry registry,
            out Vector2 localPoint)
        {
            localPoint = default;
            if (animationRoot == null) return false;

            if (!TryResolveScreenPoint(endpoint, registry, out var screenPoint))
                return false;

            var camera = ResolveCanvasCamera(animationRoot);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                animationRoot,
                screenPoint,
                camera,
                out localPoint);
        }

        private static bool TryResolveScreenPoint(
            ResourceAnimationEndpoint endpoint,
            IResourceAnimationTargetRegistry registry,
            out Vector2 screenPoint)
        {
            screenPoint = default;

            switch (endpoint.Kind)
            {
                case ResourceAnimationEndpointKind.ScreenPoint:
                    screenPoint = endpoint.ScreenPointValue;
                    return true;

                case ResourceAnimationEndpointKind.WorldPosition:
                    return TryProjectWorld(endpoint.WorldPositionValue, endpoint.Camera, out screenPoint);

                case ResourceAnimationEndpointKind.WorldTransform:
                    if (endpoint.WorldTransformValue == null) return false;
                    return TryProjectWorld(endpoint.WorldTransformValue.position, endpoint.Camera, out screenPoint);

                case ResourceAnimationEndpointKind.RectTransform:
                    return TryResolveRectCenter(endpoint.RectTransform, out screenPoint);

                case ResourceAnimationEndpointKind.RegisteredTarget:
                    if (registry == null || !registry.TryGetTarget(endpoint.TargetId, out var target))
                        return false;
                    return TryResolveRectCenter(target, out screenPoint);

                default:
                    return false;
            }
        }

        private static bool TryProjectWorld(Vector3 worldPosition, Camera camera, out Vector2 screenPoint)
        {
            screenPoint = default;

            camera ??= Camera.main;
            if (camera == null) return false;

            var projected = camera.WorldToScreenPoint(worldPosition);
            if (projected.z < 0f) return false;

            screenPoint = projected;
            return true;
        }

        private static bool TryResolveRectCenter(RectTransform rectTransform, out Vector2 screenPoint)
        {
            screenPoint = default;
            if (rectTransform == null) return false;

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var worldCenter = (corners[0] + corners[2]) * 0.5f;
            screenPoint = RectTransformUtility.WorldToScreenPoint(
                ResolveCanvasCamera(rectTransform),
                worldCenter);
            return true;
        }

        private static Camera ResolveCanvasCamera(RectTransform rectTransform)
        {
            var canvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }
    }
}
