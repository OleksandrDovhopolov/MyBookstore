using UnityEngine;

namespace Game.UI.ContentWidget
{
    public enum ContentWidgetPlacementSide
    {
        Above,
        Below,
        Left,
        Right
    }

    public readonly struct ContentWidgetPlacementResult
    {
        public ContentWidgetPlacementResult(Vector2 position, ContentWidgetPlacementSide side)
        {
            Position = position;
            Side = side;
        }

        public Vector2 Position { get; }
        public ContentWidgetPlacementSide Side { get; }
    }

    public static class ContentWidgetPlacement
    {
        public static ContentWidgetPlacementResult Resolve(
            Rect anchorInParent,
            Vector2 widgetSize,
            Rect parentRect,
            float verticalOffset,
            float horizontalOffset,
            float padding,
            float verticalZoneRatio,
            Vector2 pivot)
        {
            var safeMinX = parentRect.xMin + padding;
            var safeMaxX = parentRect.xMax - padding;
            var safeMinY = parentRect.yMin + padding;
            var safeMaxY = parentRect.yMax - padding;

            var clampedSize = new Vector2(
                Mathf.Max(0f, widgetSize.x),
                Mathf.Max(0f, widgetSize.y));

            var minPositionX = safeMinX + clampedSize.x * pivot.x;
            var maxPositionX = safeMaxX - clampedSize.x * (1f - pivot.x);
            var minPositionY = safeMinY + clampedSize.y * pivot.y;
            var maxPositionY = safeMaxY - clampedSize.y * (1f - pivot.y);

            var anchorCenterX = (anchorInParent.xMin + anchorInParent.xMax) * 0.5f;
            var anchorCenterY = (anchorInParent.yMin + anchorInParent.yMax) * 0.5f;
            var preferredSide = anchorCenterX >= parentRect.center.x
                ? ContentWidgetPlacementSide.Left
                : ContentWidgetPlacementSide.Right;
            var oppositeSide = preferredSide == ContentWidgetPlacementSide.Left
                ? ContentWidgetPlacementSide.Right
                : ContentWidgetPlacementSide.Left;
            var priority = BuildPriority(
                anchorCenterY,
                parentRect,
                verticalZoneRatio,
                preferredSide,
                oppositeSide);

            for (var i = 0; i < priority.Length; i++)
            {
                var candidateSide = priority[i];
                var candidate = CreateCandidate(
                    candidateSide,
                    anchorInParent,
                    anchorCenterX,
                    anchorCenterY,
                    clampedSize,
                    pivot,
                    verticalOffset,
                    horizontalOffset);

                if (!FitsPrimaryAxis(candidateSide, candidate, clampedSize, pivot, safeMinX, safeMaxX, safeMinY, safeMaxY))
                    continue;

                return new ContentWidgetPlacementResult(
                    ClampPosition(candidate, minPositionX, maxPositionX, minPositionY, maxPositionY),
                    candidateSide);
            }

            var side = priority[0];
            var fallback = CreateCandidate(
                side,
                anchorInParent,
                anchorCenterX,
                anchorCenterY,
                clampedSize,
                pivot,
                verticalOffset,
                horizontalOffset);

            return new ContentWidgetPlacementResult(
                ClampPosition(fallback, minPositionX, maxPositionX, minPositionY, maxPositionY),
                side);
        }

        private static ContentWidgetPlacementSide[] BuildPriority(
            float anchorCenterY,
            Rect parentRect,
            float verticalZoneRatio,
            ContentWidgetPlacementSide preferredSide,
            ContentWidgetPlacementSide oppositeSide)
        {
            var clampedZoneRatio = Mathf.Clamp(verticalZoneRatio, 0f, 0.5f);
            var verticalPosition = parentRect.height > 0f
                ? Mathf.Clamp01((anchorCenterY - parentRect.yMin) / parentRect.height)
                : 0.5f;

            if (verticalPosition <= clampedZoneRatio)
            {
                return new[]
                {
                    ContentWidgetPlacementSide.Above,
                    ContentWidgetPlacementSide.Below,
                    preferredSide,
                    oppositeSide
                };
            }

            if (verticalPosition >= 1f - clampedZoneRatio)
            {
                return new[]
                {
                    ContentWidgetPlacementSide.Below,
                    ContentWidgetPlacementSide.Above,
                    preferredSide,
                    oppositeSide
                };
            }

            return new[]
            {
                preferredSide,
                oppositeSide,
                ContentWidgetPlacementSide.Above,
                ContentWidgetPlacementSide.Below
            };
        }

        private static Vector2 CreateCandidate(
            ContentWidgetPlacementSide side,
            Rect anchorInParent,
            float anchorCenterX,
            float anchorCenterY,
            Vector2 widgetSize,
            Vector2 pivot,
            float verticalOffset,
            float horizontalOffset)
        {
            var centeredX = anchorCenterX + (pivot.x - 0.5f) * widgetSize.x;
            var centeredY = anchorCenterY + (pivot.y - 0.5f) * widgetSize.y;

            return side switch
            {
                ContentWidgetPlacementSide.Above => new Vector2(
                    centeredX,
                    anchorInParent.yMax + verticalOffset + widgetSize.y * pivot.y),
                ContentWidgetPlacementSide.Below => new Vector2(
                    centeredX,
                    anchorInParent.yMin - verticalOffset - widgetSize.y * (1f - pivot.y)),
                ContentWidgetPlacementSide.Left => new Vector2(
                    anchorInParent.xMin - horizontalOffset - widgetSize.x * (1f - pivot.x),
                    centeredY),
                ContentWidgetPlacementSide.Right => new Vector2(
                    anchorInParent.xMax + horizontalOffset + widgetSize.x * pivot.x,
                    centeredY),
                _ => new Vector2(centeredX, centeredY)
            };
        }

        private static bool FitsPrimaryAxis(
            ContentWidgetPlacementSide side,
            Vector2 position,
            Vector2 widgetSize,
            Vector2 pivot,
            float safeMinX,
            float safeMaxX,
            float safeMinY,
            float safeMaxY)
        {
            var minX = position.x - widgetSize.x * pivot.x;
            var maxX = position.x + widgetSize.x * (1f - pivot.x);
            var minY = position.y - widgetSize.y * pivot.y;
            var maxY = position.y + widgetSize.y * (1f - pivot.y);

            switch (side)
            {
                case ContentWidgetPlacementSide.Above:
                case ContentWidgetPlacementSide.Below:
                    return minY >= safeMinY && maxY <= safeMaxY;
                case ContentWidgetPlacementSide.Left:
                case ContentWidgetPlacementSide.Right:
                    return minX >= safeMinX && maxX <= safeMaxX;
                default:
                    return false;
            }
        }

        private static Vector2 ClampPosition(
            Vector2 position,
            float minPositionX,
            float maxPositionX,
            float minPositionY,
            float maxPositionY)
        {
            return new Vector2(
                ClampEvenIfInverted(position.x, minPositionX, maxPositionX),
                ClampEvenIfInverted(position.y, minPositionY, maxPositionY));
        }

        private static float ClampEvenIfInverted(float value, float min, float max)
        {
            if (min <= max)
                return Mathf.Clamp(value, min, max);

            return (min + max) * 0.5f;
        }
    }
}
