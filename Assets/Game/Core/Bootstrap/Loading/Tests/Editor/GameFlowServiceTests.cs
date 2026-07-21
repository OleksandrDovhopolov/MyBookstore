using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Bootstrap.Loading;
using Game.LocationVisits.API;
using Game.Tutorial.API;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer.Unity;

namespace Game.Bootstrap.Loading.Tests.Editor
{
    public sealed class GameFlowServiceTests
    {
        [UnityTest]
        public IEnumerator EnterLocationAsync_RecordsVisitAfterReveal()
        {
            using var harness = new Harness();
            var revealStarted = false;
            harness.Animation.OnRevealStarted = () =>
            {
                revealStarted = true;
                Assert.That(harness.Visits.RecordedLocationIds, Is.Empty);
            };

            yield return ToCoroutine(harness.Flow.EnterLocationAsync("loc_downtown", CancellationToken.None));

            Assert.That(revealStarted, Is.True);
            Assert.That(harness.SceneTransition.AdditiveLoadCount, Is.EqualTo(1));
            Assert.That(harness.Animation.RevealCount, Is.EqualTo(1));
            Assert.That(harness.Visits.RecordedLocationIds, Is.EqualTo(new[] { "loc_downtown" }));
        }

        [UnityTest]
        public IEnumerator EnterLocationAsync_DoesNotRecordVisit_WhenRevealIsCanceled()
        {
            using var harness = new Harness();
            harness.Animation.CancelOnReveal = true;

            var task = harness.Flow.EnterLocationAsync("loc_downtown", CancellationToken.None).AsTask();
            while (!task.IsCompleted)
                yield return null;

            Assert.That(IsOperationCanceled(task), Is.True);
            Assert.That(harness.Visits.RecordedLocationIds, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EnterLocationAsync_DoesNotRecordVisit_WhenLoadFails()
        {
            using var harness = new Harness();
            harness.SceneTransition.ThrowOnLoad = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[GameFlow\] EnterLocationAsync failed"));

            var task = harness.Flow.EnterLocationAsync("loc_downtown", CancellationToken.None).AsTask();
            while (!task.IsCompleted)
                yield return null;

            Assert.That(task.IsFaulted, Is.True);
            Assert.That(harness.Visits.RecordedLocationIds, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EnterLocationAsync_DoesNotRecordSecondVisit_WhenAlreadyLoaded()
        {
            using var harness = new Harness();
            yield return ToCoroutine(harness.Flow.EnterLocationAsync("loc_downtown", CancellationToken.None));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[GameFlow\] EnterLocation ignored"));
            yield return ToCoroutine(harness.Flow.EnterLocationAsync("loc_downtown", CancellationToken.None));

            Assert.That(harness.Visits.RecordedLocationIds, Is.EqualTo(new[] { "loc_downtown" }));
        }

        [UnityTest]
        public IEnumerator EnterLocationAsync_DoesNotGateTutorialAutoStart()
        {
            using var harness = new Harness();

            yield return ToCoroutine(harness.Flow.EnterLocationAsync("loc_downtown", CancellationToken.None));

            Assert.That(harness.TutorialGate.BlockCount, Is.EqualTo(0));
            Assert.That(harness.TutorialGate.ReleaseCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ReturnToHubAsync_BlocksGateUntilRevealCompletes()
        {
            using var harness = new Harness();
            harness.SetLocationLoaded(true);
            harness.Animation.OnRevealStarted = () =>
            {
                Assert.That(harness.TutorialGate.IsBlocked, Is.True);
                Assert.That(harness.TutorialGate.ReleaseCount, Is.EqualTo(0));
            };

            yield return ToCoroutine(harness.Flow.ReturnToHubAsync(CancellationToken.None));

            Assert.That(harness.Animation.RevealCount, Is.EqualTo(1));
            Assert.That(harness.TutorialGate.BlockCount, Is.EqualTo(1));
            Assert.That(harness.TutorialGate.ReleaseCount, Is.EqualTo(1));
            Assert.That(harness.TutorialGate.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ReturnToHubAsync_ReleasesGate_WhenRevealIsCanceled()
        {
            using var harness = new Harness();
            harness.SetLocationLoaded(true);
            harness.Animation.CancelOnReveal = true;

            var task = harness.Flow.ReturnToHubAsync(CancellationToken.None).AsTask();
            while (!task.IsCompleted)
                yield return null;

            Assert.That(IsOperationCanceled(task), Is.True);
            Assert.That(harness.TutorialGate.BlockCount, Is.EqualTo(1));
            Assert.That(harness.TutorialGate.ReleaseCount, Is.EqualTo(1));
            Assert.That(harness.TutorialGate.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ReturnToHubAsync_ReleasesGate_WhenUnloadFails()
        {
            using var harness = new Harness();
            harness.SetLocationLoaded(true);
            harness.SceneTransition.ThrowOnUnload = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[GameFlow\] ReturnToHubAsync failed"));

            var task = harness.Flow.ReturnToHubAsync(CancellationToken.None).AsTask();
            while (!task.IsCompleted)
                yield return null;

            Assert.That(task.IsFaulted, Is.True);
            Assert.That(harness.TutorialGate.BlockCount, Is.EqualTo(1));
            Assert.That(harness.TutorialGate.ReleaseCount, Is.EqualTo(1));
            Assert.That(harness.TutorialGate.IsBlocked, Is.False);
        }

        private static IEnumerator ToCoroutine(UniTask task)
        {
            var wrappedTask = task.AsTask();
            while (!wrappedTask.IsCompleted)
                yield return null;

            if (wrappedTask.IsFaulted)
                throw wrappedTask.Exception;
            if (wrappedTask.IsCanceled)
                throw new OperationCanceledException();
        }

        private static bool IsOperationCanceled(System.Threading.Tasks.Task task)
        {
            if (task.IsCanceled) return true;
            return task.Exception?.GetBaseException() is OperationCanceledException;
        }

        private sealed class Harness : IDisposable
        {
            private readonly GameObject _scopeRoot;
            private readonly GameObject _hubRoot;
            private readonly GameFlowSettings _settings;

            public Harness()
            {
                SceneTransition = new FakeSceneTransitionService();
                Animation = new FakeTransitionAnimationService();
                Visits = new FakeLocationVisitService();
                TutorialGate = new FakeTutorialAutoStartGate();
                _settings = ScriptableObject.CreateInstance<GameFlowSettings>();

                Flow = new GameFlowService(SceneTransition, Animation, _settings, Visits, TutorialGate);

                var scope = LifetimeScope.Create(_ => { }, "Test LifetimeScope");
                _scopeRoot = scope.gameObject;
                var globalScopeField = typeof(GameFlowService)
                    .GetField("_globalScope", BindingFlags.Instance | BindingFlags.NonPublic);
                if (globalScopeField == null)
                    throw new MissingFieldException(nameof(GameFlowService), "_globalScope");

                globalScopeField.SetValue(Flow, scope);

                _hubRoot = new GameObject("Test HubRoot");
                Flow.RegisterHubRoot(_hubRoot);
            }

            public GameFlowService Flow { get; }
            public FakeSceneTransitionService SceneTransition { get; }
            public FakeTransitionAnimationService Animation { get; }
            public FakeLocationVisitService Visits { get; }
            public FakeTutorialAutoStartGate TutorialGate { get; }

            public void SetLocationLoaded(bool loaded)
            {
                var field = typeof(GameFlowService)
                    .GetField("_locationLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                    throw new MissingFieldException(nameof(GameFlowService), "_locationLoaded");

                field.SetValue(Flow, loaded);
            }

            public void Dispose()
            {
                if (_hubRoot != null) UnityEngine.Object.DestroyImmediate(_hubRoot);
                if (_scopeRoot != null) UnityEngine.Object.DestroyImmediate(_scopeRoot);
                if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            }
        }

        private sealed class FakeSceneTransitionService : ISceneTransitionService
        {
            public int AdditiveLoadCount { get; private set; }
            public bool ThrowOnLoad { get; set; }
            public bool ThrowOnUnload { get; set; }

            public UniTask TransitionToAsync(string sceneName, IProgress<float> progress, CancellationToken ct)
                => UniTask.CompletedTask;

            public UniTask<Scene> LoadAdditiveAsync(string sceneName, bool makeActive, IProgress<float> progress, CancellationToken ct)
            {
                AdditiveLoadCount++;
                if (ThrowOnLoad)
                    throw new InvalidOperationException("load failed");

                return UniTask.FromResult(default(Scene));
            }

            public UniTask UnloadAsync(string sceneName, CancellationToken ct)
            {
                if (ThrowOnUnload)
                    throw new InvalidOperationException("unload failed");

                return UniTask.CompletedTask;
            }
            public void SetActiveScene(string sceneName) { }
        }

        private sealed class FakeTransitionAnimationService : ITransitionAnimationService
        {
            public int RevealCount { get; private set; }
            public bool CancelOnReveal { get; set; }
            public Action OnRevealStarted { get; set; }

            public UniTask PlayCoverAsync(CancellationToken ct) => UniTask.CompletedTask;

            public async UniTask PlayRevealAsync(CancellationToken ct)
            {
                RevealCount++;
                OnRevealStarted?.Invoke();
                if (CancelOnReveal)
                    throw new OperationCanceledException(ct);

                await UniTask.CompletedTask;
            }
        }

        private sealed class FakeLocationVisitService : ILocationVisitService
        {
            public List<string> RecordedLocationIds { get; } = new();
            public int ClearCount { get; private set; }

            public void RecordVisit(string locationId) => RecordedLocationIds.Add(locationId);
            public void ClearCurrentLocation() => ClearCount++;
        }

        private sealed class FakeTutorialAutoStartGate : ITutorialAutoStartGate
        {
            private int _blockCount;

            public bool IsBlocked => _blockCount > 0;
            public int BlockCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public event Action Released;

            public void Block()
            {
                BlockCount++;
                _blockCount++;
            }

            public void Release()
            {
                ReleaseCount++;
                _blockCount--;
                if (_blockCount == 0)
                    Released?.Invoke();
            }
        }
    }
}
