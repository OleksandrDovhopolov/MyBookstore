using UnityEngine;

namespace Game.UI.ContentWidget
{
    public static class ContentWidgetPlacement
    {
        public static Vector2 Resolve(
            Rect anchorInParent,
            Vector2 widgetSize,
            Rect parentRect,
            float offset,
            float padding,
            Vector2 pivot,
            out bool placedBelow)
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
            var x = ClampEvenIfInverted(anchorCenterX, minPositionX, maxPositionX);

            var aboveY = anchorInParent.yMax + offset + clampedSize.y * pivot.y;
            var aboveTop = aboveY + clampedSize.y * (1f - pivot.y);
            if (aboveTop <= safeMaxY)
            {
                placedBelow = false;
                return new Vector2(x, ClampEvenIfInverted(aboveY, minPositionY, maxPositionY));
            }

            placedBelow = true;
            var belowY = anchorInParent.yMin - offset - clampedSize.y * (1f - pivot.y);
            return new Vector2(x, ClampEvenIfInverted(belowY, minPositionY, maxPositionY));
        }

        private static float ClampEvenIfInverted(float value, float min, float max)
        {
            if (min <= max)
                return Mathf.Clamp(value, min, max);

            return (min + max) * 0.5f;
        }
    }
}
