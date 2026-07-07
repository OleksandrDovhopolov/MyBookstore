using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.ContentWidget;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Core.UI.Tests.Editor.ContentWidget
{
    public sealed class ContentWidgetViewTests
    {
        [TearDown]
        public void TearDown()
        {
            WidgetRegistry.Clear();
        }

        [Test]
        public void RequestClose_RaisesCloseClick()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.View.RequestClose();

            Assert.IsTrue(closed);
        }

        [UnityTest]
        public IEnumerator ShowContentView_DoesNotAutoClose_WhenDelayIsDisabled()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.Show(new TestWidgetData());
            yield return new WaitForSeconds(0.05f);

            Assert.IsFalse(closed);
        }

        [UnityTest]
        public IEnumerator ShowContentView_AutoClosesAfterDelay()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0.01f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.Show(new TestWidgetData());
            yield return new WaitForSeconds(0.05f);

            Assert.IsTrue(closed);
        }

        [UnityTest]
        public IEnumerator HideContent_CancelsAutoCloseTimer()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0.01f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.Show(new TestWidgetData());
            fixture.View.HideContent();
            yield return new WaitForSeconds(0.05f);

            Assert.IsFalse(closed);
        }

        [UnityTest]
        public IEnumerator ShowContentView_CancelsPreviousAutoCloseTimer()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0.05f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.Show(new TestWidgetData());
            yield return new WaitForSeconds(0.03f);

            fixture.Show(new TestWidgetData());
            yield return new WaitForSeconds(0.03f);

            Assert.IsFalse(closed);

            yield return new WaitForSeconds(0.04f);
            Assert.IsTrue(closed);
        }

        private sealed class ContentWidgetFixture : System.IDisposable
        {
            private readonly GameObject _root;
            private readonly GameObject _prefab;

            private ContentWidgetFixture(
                GameObject root,
                GameObject prefab,
                ContentWidgetView view,
                RectTransform anchor)
            {
                _root = root;
                _prefab = prefab;
                View = view;
                Anchor = anchor;
            }

            public ContentWidgetView View { get; }
            public RectTransform Anchor { get; }

            public static ContentWidgetFixture Create(float autoCloseDelaySeconds)
            {
                WidgetRegistry.Clear();

                var root = new GameObject("ContentWidgetTestRoot", typeof(RectTransform));
                var parent = root.GetComponent<RectTransform>();
                parent.sizeDelta = new Vector2(800f, 600f);

                var containerObject = new GameObject("Container", typeof(RectTransform));
                var container = containerObject.GetComponent<RectTransform>();
                container.SetParent(parent, false);
                container.sizeDelta = new Vector2(160f, 100f);
                container.pivot = new Vector2(0.5f, 0.5f);

                var anchorObject = new GameObject("Anchor", typeof(RectTransform));
                var anchor = anchorObject.GetComponent<RectTransform>();
                anchor.SetParent(parent, false);
                anchor.sizeDelta = new Vector2(40f, 40f);
                anchor.anchoredPosition = Vector2.zero;

                var viewObject = new GameObject(
                    "ContentWidgetView",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasGroup),
                    typeof(ContentWidgetView));
                viewObject.transform.SetParent(root.transform, false);
                var view = viewObject.GetComponent<ContentWidgetView>();

                SetSerializedField(view, "_container", container);
                SetSerializedField(view, "_autoCloseDelaySeconds", autoCloseDelaySeconds);
                SetSerializedField(view, "_verticalOffset", 12f);
                SetSerializedField(view, "_horizontalOffset", 12f);
                SetSerializedField(view, "_edgePadding", 0f);
                SetSerializedField(view, "_verticalZoneRatio", 0.33f);

                var prefab = new GameObject("TestWidgetPrefab", typeof(TestWidgetView));
                WidgetRegistry.Register<TestWidgetData>(prefab.GetComponent<TestWidgetView>());

                return new ContentWidgetFixture(root, prefab, view, anchor);
            }

            public void Show(TestWidgetData data)
            {
                View.ShowContentView(data, Anchor);
            }

            public void Dispose()
            {
                WidgetRegistry.Clear();
                Object.DestroyImmediate(_prefab);
                Object.DestroyImmediate(_root);
            }

            private static void SetSerializedField<T>(ContentWidgetView view, string name, T value)
            {
                typeof(ContentWidgetView)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(view, value);
            }
        }

        private sealed class TestWidgetData : ContentWidgetDataBase
        {
        }

        public sealed class TestWidgetView : MonoBehaviour, IContentWidgetView
        {
            public bool Setup(ContentWidgetDataBase data) => data is TestWidgetData;

            public UniTask OnViewCreatedAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
