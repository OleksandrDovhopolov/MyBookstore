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
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0.02f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.Show(new TestWidgetData());
            yield return WaitUntilOrTimeout(() => closed, 1f);

            Assert.IsTrue(closed);
        }

        [UnityTest]
        public IEnumerator ShowContentView_DoesNotAutoClose_WhenAutoCloseDisabledForThisShow()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0.01f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.Show(new TestWidgetData(), autoCloseEnabled: false);
            yield return new WaitForSeconds(0.05f);

            Assert.IsFalse(closed);
        }

        [UnityTest]
        public IEnumerator ShowContentView_AcceptsHorizontalOnlyPlacementMode()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0f);
            var closed = false;
            fixture.View.CloseClick += () => closed = true;

            fixture.Show(new TestWidgetData(), ContentWidgetPlacementMode.HorizontalOnly);
            yield return null;

            Assert.IsFalse(closed);
        }

        [UnityTest]
        public IEnumerator ShowContentView_UsesActiveViewSize_ForPlacement()
        {
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0f);
            fixture.Anchor.anchoredPosition = Vector2.zero;

            fixture.Show(new TestWidgetData(), ContentWidgetPlacementMode.HorizontalOnly);
            yield return null;

            Assert.That(fixture.Container.anchoredPosition.x, Is.EqualTo(-52f).Within(0.001f));
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
            using var fixture = ContentWidgetFixture.Create(autoCloseDelaySeconds: 0.2f);
            var closeCount = 0;
            fixture.View.CloseClick += () => closeCount++;

            fixture.Show(new TestWidgetData());
            fixture.SetAutoCloseDelay(0.5f);
            fixture.Show(new TestWidgetData());
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.AreEqual(0, closeCount);

            yield return WaitUntilOrTimeout(() => closeCount > 0, 1f);
            Assert.AreEqual(1, closeCount);
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> predicate, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private sealed class ContentWidgetFixture : System.IDisposable
        {
            private readonly GameObject _root;
            private readonly GameObject _prefab;

            private ContentWidgetFixture(
                GameObject root,
                GameObject prefab,
                ContentWidgetView view,
                RectTransform container,
                RectTransform anchor)
            {
                _root = root;
                _prefab = prefab;
                View = view;
                Container = container;
                Anchor = anchor;
            }

            public ContentWidgetView View { get; }
            public RectTransform Container { get; }
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

                var prefab = new GameObject("TestWidgetPrefab", typeof(RectTransform), typeof(TestWidgetView));
                prefab.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 30f);
                WidgetRegistry.Register<TestWidgetData>(prefab.GetComponent<TestWidgetView>());

                return new ContentWidgetFixture(root, prefab, view, container, anchor);
            }

            public void Show(TestWidgetData data)
                => Show(data, autoCloseEnabled: true);

            public void Show(TestWidgetData data, bool autoCloseEnabled)
            {
                View.ShowContentView(data, Anchor, autoCloseEnabled);
            }

            public void Show(TestWidgetData data, ContentWidgetPlacementMode placementMode)
            {
                View.ShowContentView(data, Anchor, autoCloseEnabled: true, placementMode: placementMode);
            }

            public void SetAutoCloseDelay(float seconds)
            {
                SetSerializedField(View, "_autoCloseDelaySeconds", seconds);
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
