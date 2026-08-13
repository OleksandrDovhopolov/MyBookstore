using NUnit.Framework;
using UIShared;
using UnityEditor;
using UnityEngine;

namespace GameplayUI.Tests.Editor
{
    public sealed class GameplaySceneViewGoldCounterTests
    {
        private GameObject _viewGo;
        private GameObject _hubRoot;
        private GameObject _locationRoot;

        [TearDown]
        public void TearDown()
        {
            if (_viewGo != null) Object.DestroyImmediate(_viewGo);
            if (_hubRoot != null) Object.DestroyImmediate(_hubRoot);
            if (_locationRoot != null) Object.DestroyImmediate(_locationRoot);
            ResourceCounterTargets.Clear();
        }

        [Test]
        public void SetGoldCounterMode_TogglesHubAndLocationRoots()
        {
            var view = CreateView(out _, out _);

            view.SetGoldCounterMode(locationLoaded: false);

            Assert.IsTrue(_hubRoot.activeSelf);
            Assert.IsFalse(_locationRoot.activeSelf);

            view.SetGoldCounterMode(locationLoaded: true);

            Assert.IsFalse(_hubRoot.activeSelf);
            Assert.IsTrue(_locationRoot.activeSelf);
        }

        [Test]
        public void SetLocationEarnedGold_UpdatesLocationCounterOnly()
        {
            var view = CreateView(out var hubCounter, out var locationCounter);
            hubCounter.SetAmountImmediate(50);
            locationCounter.SetAmountImmediate(0);

            view.SetLocationEarnedGold(20);

            Assert.AreEqual(50, hubCounter.DisplayedAmount);
            Assert.AreEqual(20, locationCounter.DisplayedAmount);
        }

        private GameplaySceneView CreateView(
            out ResourceCounterTargetTag hubCounter,
            out ResourceCounterTargetTag locationCounter)
        {
            ResourceCounterTargets.Clear();

            _viewGo = new GameObject("view", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var view = _viewGo.AddComponent<GameplaySceneView>();

            _hubRoot = new GameObject("hub", typeof(RectTransform));
            _hubRoot.transform.SetParent(_viewGo.transform, false);
            hubCounter = _hubRoot.AddComponent<ResourceCounterTargetTag>();

            _locationRoot = new GameObject("location", typeof(RectTransform));
            _locationRoot.transform.SetParent(_viewGo.transform, false);
            _locationRoot.SetActive(false);
            locationCounter = _locationRoot.AddComponent<ResourceCounterTargetTag>();

            var locationCounterSerialized = new SerializedObject(locationCounter);
            locationCounterSerialized.FindProperty("_registerAsResourceTarget").boolValue = false;
            locationCounterSerialized.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_hubGoldCounterRoot").objectReferenceValue = _hubRoot;
            serialized.FindProperty("_hubGoldCounter").objectReferenceValue = hubCounter;
            serialized.FindProperty("_locationGoldCounterRoot").objectReferenceValue = _locationRoot;
            serialized.FindProperty("_locationGoldCounter").objectReferenceValue = locationCounter;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }
    }
}
