using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.UI.Tests.Editor.ContentWidget
{
    public sealed class WidgetRegistryTests
    {
        private GameObject _prefabObject;
        private GameObject _replacementObject;

        [TearDown]
        public void TearDown()
        {
            WidgetRegistry.Clear();

            if (_prefabObject != null)
                Object.DestroyImmediate(_prefabObject);

            if (_replacementObject != null)
                Object.DestroyImmediate(_replacementObject);
        }

        [Test]
        public void GetPrefab_ReturnsRegisteredPrefab_ForExactDataType()
        {
            var prefab = CreatePrefab("ContentWidgetTestPrefab");

            WidgetRegistry.Register<TestWidgetData>(prefab);

            Assert.AreSame(prefab, WidgetRegistry.GetPrefab(typeof(TestWidgetData)));
        }

        [Test]
        public void GetPrefab_ReturnsNull_ForUnknownType()
        {
            Assert.IsNull(WidgetRegistry.GetPrefab(typeof(TestWidgetData)));
        }

        [Test]
        public void Register_OverwritesExistingPrefab()
        {
            var first = CreatePrefab("First");
            var second = CreateReplacementPrefab("Second");

            WidgetRegistry.Register<TestWidgetData>(first);
            WidgetRegistry.Register<TestWidgetData>(second);

            Assert.AreSame(second, WidgetRegistry.GetPrefab(typeof(TestWidgetData)));
        }

        [Test]
        public void Unregister_RemovesOnlyMatchingPrefab_WhenPrefabProvided()
        {
            var first = CreatePrefab("First");
            var second = CreateReplacementPrefab("Second");

            WidgetRegistry.Register<TestWidgetData>(first);
            WidgetRegistry.Unregister<TestWidgetData>(second);

            Assert.AreSame(first, WidgetRegistry.GetPrefab(typeof(TestWidgetData)));

            WidgetRegistry.Unregister<TestWidgetData>(first);

            Assert.IsNull(WidgetRegistry.GetPrefab(typeof(TestWidgetData)));
        }

        private TestWidgetView CreatePrefab(string name)
        {
            _prefabObject = new GameObject(name);
            return _prefabObject.AddComponent<TestWidgetView>();
        }

        private TestWidgetView CreateReplacementPrefab(string name)
        {
            _replacementObject = new GameObject(name);
            return _replacementObject.AddComponent<TestWidgetView>();
        }

        private sealed class TestWidgetData : ContentWidgetDataBase
        {
        }
    }

    public sealed class TestWidgetView : MonoBehaviour, IContentWidgetView
    {
        public bool Setup(ContentWidgetDataBase data) => true;

        public UniTask OnViewCreatedAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
