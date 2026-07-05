using Game.UI.ResourceAnimations;
using Infrastructure.ResourceAnimations;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.UI.Tests.Editor.ResourceAnimations
{
    public sealed class ResourceAnimationEndpointResolverTests
    {
        private GameObject _rootGo;
        private GameObject _targetGo;
        private GameObject _cameraGo;

        [TearDown]
        public void TearDown()
        {
            if (_rootGo != null) Object.DestroyImmediate(_rootGo);
            if (_targetGo != null) Object.DestroyImmediate(_targetGo);
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        }

        [Test]
        public void ScreenPoint_ResolvesToAnimationRootLocalPoint()
        {
            var root = CreateRoot();
            var endpoint = ResourceAnimationEndpoint.ScreenPoint(new Vector2(120f, 80f));

            var ok = ResourceAnimationEndpointResolver.TryResolve(
                endpoint,
                root,
                registry: null,
                out var localPoint);

            Assert.IsTrue(ok);
            Assert.IsTrue(IsFinite(localPoint));
        }

        [Test]
        public void RectTransform_UsesRectCenter()
        {
            var root = CreateRoot();
            var target = CreateTargetRect(root, new Vector2(40f, 25f));

            var ok = ResourceAnimationEndpointResolver.TryResolve(
                ResourceAnimationEndpoint.Rect(target),
                root,
                registry: null,
                out var localPoint);

            Assert.IsTrue(ok);
            Assert.IsTrue(IsFinite(localPoint));
        }

        [Test]
        public void WorldPosition_WithCamera_ProjectsToAnimationRoot()
        {
            var root = CreateRoot();
            var camera = CreateCamera();

            var ok = ResourceAnimationEndpointResolver.TryResolve(
                ResourceAnimationEndpoint.WorldPosition(Vector3.zero, camera),
                root,
                registry: null,
                out var localPoint);

            Assert.IsTrue(ok);
            Assert.IsTrue(IsFinite(localPoint));
        }

        [Test]
        public void RegisteredTarget_Missing_ReturnsFalse()
        {
            var root = CreateRoot();
            var registry = new ResourceAnimationTargetRegistry();

            var ok = ResourceAnimationEndpointResolver.TryResolve(
                ResourceAnimationEndpoint.RegisteredTarget("resource:Gold"),
                root,
                registry,
                out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void WorldTransform_Null_ReturnsFalse()
        {
            var root = CreateRoot();

            var ok = ResourceAnimationEndpointResolver.TryResolve(
                ResourceAnimationEndpoint.WorldTransform(null),
                root,
                registry: null,
                out _);

            Assert.IsFalse(ok);
        }

        private RectTransform CreateRoot()
        {
            _rootGo = new GameObject("root", typeof(RectTransform), typeof(Canvas));
            var canvas = _rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var rect = (RectTransform)_rootGo.transform;
            rect.sizeDelta = new Vector2(800f, 600f);
            return rect;
        }

        private RectTransform CreateTargetRect(RectTransform parent, Vector2 anchoredPosition)
        {
            _targetGo = new GameObject("target", typeof(RectTransform));
            var rect = (RectTransform)_targetGo.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(100f, 60f);
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private Camera CreateCamera()
        {
            _cameraGo = new GameObject("camera", typeof(Camera));
            var camera = _cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y);
    }
}
