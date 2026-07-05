using UnityEngine;

namespace Infrastructure.ResourceAnimations
{
    public readonly struct ResourceAnimationEndpoint
    {
        public ResourceAnimationEndpointKind Kind { get; }
        public Vector2 ScreenPointValue { get; }
        public Vector3 WorldPositionValue { get; }
        public Transform WorldTransformValue { get; }
        public Camera Camera { get; }
        public RectTransform RectTransform { get; }
        public string TargetId { get; }

        private ResourceAnimationEndpoint(
            ResourceAnimationEndpointKind kind,
            Vector2 screenPoint = default,
            Vector3 worldPosition = default,
            Transform worldTransform = null,
            Camera camera = null,
            RectTransform rectTransform = null,
            string targetId = null)
        {
            Kind = kind;
            ScreenPointValue = screenPoint;
            WorldPositionValue = worldPosition;
            WorldTransformValue = worldTransform;
            Camera = camera;
            RectTransform = rectTransform;
            TargetId = targetId;
        }

        public static ResourceAnimationEndpoint ScreenPoint(Vector2 screenPoint) =>
            new(ResourceAnimationEndpointKind.ScreenPoint, screenPoint: screenPoint);

        public static ResourceAnimationEndpoint WorldPosition(Vector3 worldPosition, Camera camera = null) =>
            new(ResourceAnimationEndpointKind.WorldPosition, worldPosition: worldPosition, camera: camera);

        public static ResourceAnimationEndpoint WorldTransform(Transform transform, Camera camera = null) =>
            new(ResourceAnimationEndpointKind.WorldTransform, worldTransform: transform, camera: camera);

        public static ResourceAnimationEndpoint Rect(RectTransform rectTransform) =>
            new(ResourceAnimationEndpointKind.RectTransform, rectTransform: rectTransform);

        public static ResourceAnimationEndpoint RegisteredTarget(string targetId) =>
            new(ResourceAnimationEndpointKind.RegisteredTarget, targetId: targetId);
    }
}
