using System.Reflection;
using Book.Sell.UI.Customer;
using NUnit.Framework;
using UnityEngine;

namespace Book.Sell.Tests.Editor.UI
{
    public sealed class CustomerVisualTests
    {
        [Test]
        public void ApplyFigureSprite_NullSprite_KeepsFallback()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Visual.ApplyFigureSprite(null);

                Assert.AreSame(fixture.Fallback, fixture.Renderer.sprite);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ApplyFigureSprite_ReplacesFigureSprite()
        {
            var fixture = CreateFixture();
            var replacement = CreateSprite("replacement");
            try
            {
                fixture.Visual.ApplyFigureSprite(replacement);

                Assert.AreSame(replacement, fixture.Renderer.sprite);
            }
            finally
            {
                DestroySprite(replacement);
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
            var root = new GameObject("CustomerVisualTest");
            var visual = root.AddComponent<CustomerVisual>();
            var renderer = root.AddComponent<SpriteRenderer>();
            var fallback = CreateSprite("fallback");
            renderer.sprite = fallback;

            var field = typeof(CustomerVisual).GetField("_figure", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(visual, renderer);

            return new Fixture(root, visual, renderer, fallback);
        }

        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(1, 1) { name = $"{name}_texture" };
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
        }

        private sealed class Fixture
        {
            private readonly GameObject _root;

            public Fixture(GameObject root, CustomerVisual visual, SpriteRenderer renderer, Sprite fallback)
            {
                _root = root;
                Visual = visual;
                Renderer = renderer;
                Fallback = fallback;
            }

            public CustomerVisual Visual { get; }
            public SpriteRenderer Renderer { get; }
            public Sprite Fallback { get; }

            public void Dispose()
            {
                DestroySprite(Fallback);
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
