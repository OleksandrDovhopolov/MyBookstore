using Game.UI.ContentWidget;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.UI.Tests.Editor.ContentWidget
{
    public sealed class ContentWidgetPlacementTests
    {
        private static readonly Rect Parent = Rect.MinMaxRect(-400f, -300f, 400f, 300f);
        private static readonly Vector2 WidgetSize = new(160f, 100f);
        private static readonly Vector2 Pivot = new(0.5f, 0.5f);
        private const float VerticalOffset = 12f;
        private const float HorizontalOffset = 12f;
        private const float Padding = 16f;
        private const float VerticalZoneRatio = 0.33f;

        [Test]
        public void Resolve_PlacesAboveAnchor_WhenAnchorIsInBottomZone()
        {
            var anchor = Rect.MinMaxRect(-40f, -260f, 40f, -220f);

            var result = Resolve(anchor);

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Above));
            Assert.That(RectOf(result.Position).yMin, Is.GreaterThanOrEqualTo(anchor.yMax + VerticalOffset));
        }

        [Test]
        public void Resolve_PlacesBelowAnchor_WhenAnchorIsInTopZone()
        {
            var anchor = Rect.MinMaxRect(-40f, 220f, 40f, 260f);

            var result = Resolve(anchor);

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Below));
            Assert.That(RectOf(result.Position).yMax, Is.LessThanOrEqualTo(anchor.yMin - VerticalOffset));
        }

        [Test]
        public void Resolve_PlacesLeft_WhenMiddleAnchorIsOnRightSide()
        {
            var anchor = Rect.MinMaxRect(330f, -20f, 370f, 20f);

            var result = Resolve(anchor);

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Left));
            Assert.That(RectOf(result.Position).xMax, Is.LessThanOrEqualTo(anchor.xMin - HorizontalOffset));
        }

        [Test]
        public void Resolve_PlacesRight_WhenMiddleAnchorIsOnLeftSide()
        {
            var anchor = Rect.MinMaxRect(-370f, -20f, -330f, 20f);

            var result = Resolve(anchor);

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Right));
            Assert.That(RectOf(result.Position).xMin, Is.GreaterThanOrEqualTo(anchor.xMax + HorizontalOffset));
        }

        [Test]
        public void Resolve_FallsBackToVerticalCandidate_WhenMiddleSideCandidatesDoNotFit()
        {
            var anchor = Rect.MinMaxRect(-10f, -20f, 10f, 20f);
            var oversizedForSide = new Vector2(760f, 100f);

            var result = Resolve(anchor, oversizedForSide);

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Above));
            Assert.That(RectOf(result.Position, oversizedForSide).yMin, Is.GreaterThanOrEqualTo(anchor.yMax + VerticalOffset));
        }

        [Test]
        public void Resolve_ClampsVertically_ForSidePlacement()
        {
            var anchor = Rect.MinMaxRect(330f, 60f, 370f, 100f);
            var tallWidget = new Vector2(160f, 500f);

            var result = Resolve(anchor, tallWidget);
            var rect = RectOf(result.Position, tallWidget);

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Left));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(Parent.yMax - Padding));
        }

        [Test]
        public void Resolve_ClampsHorizontally_ForVerticalPlacement()
        {
            var anchor = Rect.MinMaxRect(360f, -260f, 400f, -220f);

            var result = Resolve(anchor);
            var rect = RectOf(result.Position);

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Above));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(Parent.xMax - Padding));
        }

        [Test]
        public void Resolve_CentersSidePlacement_WithNonCenteredPivot()
        {
            var anchor = Rect.MinMaxRect(330f, -20f, 370f, 20f);
            var pivot = new Vector2(0.25f, 0.75f);

            var result = Resolve(anchor, WidgetSize, pivot);
            var rect = RectOf(result.Position, WidgetSize, pivot);
            var widgetCenterY = (rect.yMin + rect.yMax) * 0.5f;
            var anchorCenterY = (anchor.yMin + anchor.yMax) * 0.5f;

            Assert.That(result.Side, Is.EqualTo(ContentWidgetPlacementSide.Left));
            Assert.That(widgetCenterY, Is.EqualTo(anchorCenterY).Within(0.001f));
        }

        [Test]
        public void Resolve_ReturnsFiniteClampedPosition_WhenWidgetIsLargerThanSafeRect()
        {
            var anchor = Rect.MinMaxRect(-40f, -260f, 40f, -220f);
            var oversizedWidget = new Vector2(1200f, 900f);

            var result = Resolve(anchor, oversizedWidget);

            Assert.IsFalse(float.IsNaN(result.Position.x));
            Assert.IsFalse(float.IsNaN(result.Position.y));
            Assert.IsFalse(float.IsInfinity(result.Position.x));
            Assert.IsFalse(float.IsInfinity(result.Position.y));
            Assert.That(result.Position.x, Is.EqualTo(Parent.center.x).Within(0.001f));
            Assert.That(result.Position.y, Is.EqualTo(Parent.center.y).Within(0.001f));
        }

        private static ContentWidgetPlacementResult Resolve(
            Rect anchor,
            Vector2? widgetSize = null,
            Vector2? pivot = null)
        {
            return ContentWidgetPlacement.Resolve(
                anchor,
                widgetSize ?? WidgetSize,
                Parent,
                VerticalOffset,
                HorizontalOffset,
                Padding,
                VerticalZoneRatio,
                pivot ?? Pivot);
        }

        private static Rect RectOf(Vector2 position)
        {
            return RectOf(position, WidgetSize, Pivot);
        }

        private static Rect RectOf(Vector2 position, Vector2 widgetSize, Vector2? pivot = null)
        {
            var resolvedPivot = pivot ?? Pivot;
            return Rect.MinMaxRect(
                position.x - widgetSize.x * resolvedPivot.x,
                position.y - widgetSize.y * resolvedPivot.y,
                position.x + widgetSize.x * (1f - resolvedPivot.x),
                position.y + widgetSize.y * (1f - resolvedPivot.y));
        }
    }
}
