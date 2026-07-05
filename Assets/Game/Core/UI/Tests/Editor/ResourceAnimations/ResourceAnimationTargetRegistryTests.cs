using Infrastructure.ResourceAnimations;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.UI.Tests.Editor.ResourceAnimations
{
    public sealed class ResourceAnimationTargetRegistryTests
    {
        private GameObject _go;
        private GameObject _replacementGo;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_replacementGo != null) Object.DestroyImmediate(_replacementGo);
        }

        [Test]
        public void Register_Target_CanBeResolved()
        {
            var registry = new ResourceAnimationTargetRegistry();
            var rect = CreateRect("target");

            registry.Register("resource:Gold", rect);

            Assert.IsTrue(registry.TryGetTarget("resource:Gold", out var resolved));
            Assert.AreSame(rect, resolved);
        }

        [Test]
        public void Register_SameId_ReplacesTarget()
        {
            var registry = new ResourceAnimationTargetRegistry();
            var first = CreateRect("first");
            var second = CreateReplacementRect("second");

            registry.Register("resource:Gold", first);
            registry.Register("resource:Gold", second);

            Assert.IsTrue(registry.TryGetTarget("resource:Gold", out var resolved));
            Assert.AreSame(second, resolved);
        }

        [Test]
        public void Unregister_DifferentTarget_DoesNotRemoveReplacement()
        {
            var registry = new ResourceAnimationTargetRegistry();
            var first = CreateRect("first");
            var second = CreateReplacementRect("second");

            registry.Register("resource:Gold", first);
            registry.Register("resource:Gold", second);
            registry.Unregister("resource:Gold", first);

            Assert.IsTrue(registry.TryGetTarget("resource:Gold", out var resolved));
            Assert.AreSame(second, resolved);
        }

        [Test]
        public void TryGetTarget_Missing_ReturnsFalse()
        {
            var registry = new ResourceAnimationTargetRegistry();

            Assert.IsFalse(registry.TryGetTarget("missing", out var resolved));
            Assert.IsNull(resolved);
        }

        private RectTransform CreateRect(string name)
        {
            _go = new GameObject(name, typeof(RectTransform));
            return (RectTransform)_go.transform;
        }

        private RectTransform CreateReplacementRect(string name)
        {
            _replacementGo = new GameObject(name, typeof(RectTransform));
            return (RectTransform)_replacementGo.transform;
        }
    }
}
