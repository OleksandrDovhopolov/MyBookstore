using UIShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Core.UI.Tests.Editor.ResourceCounters
{
    public sealed class ResourceCounterTargetRegistryTests
    {
        private GameObject _firstGo;
        private GameObject _secondGo;

        [SetUp]
        public void SetUp()
        {
            ResourceCounterTargets.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ResourceCounterTargets.Clear();
            if (_firstGo != null) Object.DestroyImmediate(_firstGo);
            if (_secondGo != null) Object.DestroyImmediate(_secondGo);
        }

        [Test]
        public void Register_Target_CanBeResolved()
        {
            var registry = new ResourceCounterTargetRegistry();
            var target = CreateTarget("Gold", "first");

            registry.Register(target);

            Assert.IsTrue(registry.TryGetTarget("Gold", out var resolved));
            Assert.AreSame(target, resolved);
        }

        [Test]
        public void Register_SameResource_ReplacesTarget_AndEmitsEvent()
        {
            var registry = new ResourceCounterTargetRegistry();
            var first = CreateTarget("Gold", "first");
            var second = CreateReplacementTarget("Gold", "second");
            var eventCount = 0;
            IResourceCounterTarget lastRegistered = null;
            registry.TargetRegistered += target =>
            {
                eventCount++;
                lastRegistered = target;
            };

            registry.Register(first);
            registry.Register(second);

            Assert.AreEqual(2, eventCount);
            Assert.AreSame(second, lastRegistered);
            Assert.IsTrue(registry.TryGetTarget("Gold", out var resolved));
            Assert.AreSame(second, resolved);
        }

        [Test]
        public void Unregister_DifferentTarget_DoesNotRemoveReplacement()
        {
            var registry = new ResourceCounterTargetRegistry();
            var first = CreateTarget("Gold", "first");
            var second = CreateReplacementTarget("Gold", "second");

            registry.Register(first);
            registry.Register(second);
            registry.Unregister(first);

            Assert.IsTrue(registry.TryGetTarget("Gold", out var resolved));
            Assert.AreSame(second, resolved);
        }

        [Test]
        public void Facade_Clear_DisablesLookup()
        {
            var registry = new ResourceCounterTargetRegistry();
            var target = CreateTarget("Gold", "facade");

            ResourceCounterTargets.Bind(registry);
            ResourceCounterTargets.Register(target);
            ResourceCounterTargets.Clear(registry);

            Assert.IsFalse(ResourceCounterTargets.TryGetTarget("Gold", out var resolved));
            Assert.IsNull(resolved);
        }

        [Test]
        public void TargetTag_DisplayOnly_DoesNotRegister()
        {
            var registry = new ResourceCounterTargetRegistry();
            ResourceCounterTargets.Bind(registry);

            _firstGo = new GameObject("display-only", typeof(RectTransform));
            _firstGo.SetActive(false);
            var target = _firstGo.AddComponent<ResourceCounterTargetTag>();
            var serialized = new SerializedObject(target);
            serialized.FindProperty("_resourceId").stringValue = "Gold";
            serialized.FindProperty("_registerAsResourceTarget").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _firstGo.SetActive(true);

            Assert.IsFalse(registry.TryGetTarget("Gold", out _));
        }

        private FakeCounterTarget CreateTarget(string resourceId, string name)
        {
            _firstGo = new GameObject(name, typeof(RectTransform));
            return new FakeCounterTarget(resourceId, (RectTransform)_firstGo.transform);
        }

        private FakeCounterTarget CreateReplacementTarget(string resourceId, string name)
        {
            _secondGo = new GameObject(name, typeof(RectTransform));
            return new FakeCounterTarget(resourceId, (RectTransform)_secondGo.transform);
        }
    }
}
