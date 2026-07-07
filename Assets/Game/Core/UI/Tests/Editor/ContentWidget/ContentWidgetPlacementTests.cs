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

        [Test]
        public void Resolve_PlacesAboveAnchor_WhenThereIsSpace()
        {
            var anchor = Rect.MinMaxRect(-40f, -20f, 40f, 20f);

            var position = ContentWidgetPlacement.Resolve(
                anchor,
                WidgetSize,
                Parent,
                offset: 12f,
                padding: 16f,
                Pivot,
                out var placedBelow);

            Assert.IsFalse(placedBelow);
            Assert.That(position.y - WidgetSize.y * Pivot.y, Is.GreaterThanOrEqualTo(anchor.yMax + 12f));
        }

        [Test]
        public void Resolve_FlipsBelowAnchor_WhenAboveWouldLeaveParent()
        {
            var anchor = Rect.MinMaxRect(-40f, 250f, 40f, 285f);

            var position = ContentWidgetPlacement.Resolve(
                anchor,
                WidgetSize,
                Parent,
                offset: 12f,
                padding: 16f,
                Pivot,
                out var placedBelow);

            Assert.IsTrue(placedBelow);
            Assert.That(position.y + WidgetSize.y * (1f - Pivot.y), Is.LessThanOrEqualTo(anchor.yMin - 12f));
        }

        [Test]
        public void Resolve_ClampsHorizontally_AtLeftEdge()
        {
            var anchor = Rect.MinMaxRect(-395f, -20f, -355f, 20f);

            var position = ContentWidgetPlacement.Resolve(
                anchor,
                WidgetSize,
                Parent,
                offset: 12f,
                padding: 16f,
                Pivot,
                out _);

            Assert.That(position.x - WidgetSize.x * Pivot.x, Is.GreaterThanOrEqualTo(Parent.xMin + 16f));
        }

        [Test]
        public void Resolve_ClampsHorizontally_AtRightEdge()
        {
            var anchor = Rect.MinMaxRect(355f, -20f, 395f, 20f);

            var position = ContentWidgetPlacement.Resolve(
                anchor,
                WidgetSize,
                Parent,
                offset: 12f,
                padding: 16f,
                Pivot,
                out _);

            Assert.That(position.x + WidgetSize.x * (1f - Pivot.x), Is.LessThanOrEqualTo(Parent.xMax - 16f));
        }
    }
}
