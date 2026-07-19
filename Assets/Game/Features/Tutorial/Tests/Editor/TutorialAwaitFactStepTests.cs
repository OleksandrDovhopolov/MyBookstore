using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Tutorial.Content;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.TutorialUI;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialAwaitFactStepTests
    {
        [Test]
        public async Task FactAlreadyTrue_CompletesImmediately()
        {
            var step = new TutorialAwaitFactStep("already_true", () => true, TimeSpan.FromSeconds(1));

            await step.ExecuteAsync(CancellationToken.None);

            Assert.Pass();
        }

        [Test]
        public async Task FactBecomesTrue_Completes()
        {
            var fact = false;
            var step = new TutorialAwaitFactStep("later_true", () => fact, TimeSpan.FromSeconds(1));

            var run = step.ExecuteAsync(CancellationToken.None);
            await UniTask.Yield(PlayerLoopTiming.Update);
            fact = true;
            await run;

            Assert.Pass();
        }

        [Test]
        public async Task NoTimeout_WaitsUntilFactBecomesTrue()
        {
            var fact = false;
            var step = new TutorialAwaitFactStep("no_timeout", () => fact);

            var run = step.ExecuteAsync(CancellationToken.None);
            await UniTask.Yield(PlayerLoopTiming.Update);
            fact = true;
            await run;

            Assert.Pass();
        }

        [Test]
        public async Task Timeout_AutoAdvances()
        {
            var step = new TutorialAwaitFactStep("timeout", () => false, TimeSpan.FromMilliseconds(10));

            LogAssert.Expect(LogType.Warning, new Regex(@"\[Tutorial\] await fact step 'timeout' timed out; auto-advancing\."));
            await step.ExecuteAsync(CancellationToken.None);

            Assert.Pass();
        }
    }

    public sealed class TutorialShowCalloutStepTests
    {
        [Test]
        public async Task EmptyLazyText_DoesNotCreateOverlay()
        {
            var root = new GameObject("TutorialShowCalloutStepTests_Root");
            try
            {
                var overlay = new TutorialOverlayController(
                    new FakeCanvasRoot(root.transform),
                    TutorialOverlaySettings.CreateDefault());
                var step = new TutorialShowCalloutStep("empty", overlay, () => null, "bottom");

                await step.ExecuteAsync(CancellationToken.None);

                Assert.IsNull(root.transform.Find("TutorialOverlayRoot"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task LazyText_IsReadOnExecute()
        {
            var root = new GameObject("TutorialShowCalloutStepTests_Root");
            try
            {
                var text = (string)null;
                var overlay = new TutorialOverlayController(
                    new FakeCanvasRoot(root.transform),
                    TutorialOverlaySettings.CreateDefault());
                var step = new TutorialShowCalloutStep("lazy", overlay, () => text, "bottom");

                text = "Now known";
                await step.ExecuteAsync(CancellationToken.None);

                Assert.IsNotNull(root.transform.Find("TutorialOverlayRoot"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class FakeCanvasRoot : IUICanvasRoot
        {
            public FakeCanvasRoot(Transform root)
            {
                HudRoot = root;
                WindowsRoot = root;
            }

            public Transform HudRoot { get; }
            public Transform WindowsRoot { get; }
            public GameObject Blocker => null;
            public MonoBehaviour TransitionAnimation => null;
        }
    }

    public sealed class TutorialBlockingCalloutStepTests
    {
        [Test]
        public async Task HappyPath_PublishesPauseSymmetrically_AndLeavesPanelVisible()
        {
            var h = new OverlayHarness("TutorialBlockingCalloutStepTests_Root");
            try
            {
                var ui = new FakeUIManager();
                var publisher = new RecordingPublisher<SalesPauseRequested>();
                var step = new TutorialBlockingCalloutStep(
                    "blocking",
                    ui,
                    h.Overlay,
                    publisher,
                    () => true,
                    () => "Search text",
                    "bottom");

                var run = step.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.AreEqual(1, publisher.Messages.Count);
                Assert.IsTrue(publisher.Messages[0].Paused);
                Assert.IsTrue(ui.HasManualLock);

                h.Blackout.OnPointerClick(null);
                await run;

                Assert.AreEqual(2, publisher.Messages.Count);
                Assert.IsFalse(publisher.Messages[1].Paused);
                Assert.IsFalse(ui.HasManualLock);
                Assert.IsFalse(h.Blackout.gameObject.activeSelf);
                Assert.IsTrue(h.TextPanel.gameObject.activeSelf);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async Task GateFalse_DoesNotCreateOverlayOrPublishPause()
        {
            var h = new OverlayHarness("TutorialBlockingCalloutStepTests_Root");
            try
            {
                var publisher = new RecordingPublisher<SalesPauseRequested>();
                var step = new TutorialBlockingCalloutStep(
                    "gate_false",
                    new FakeUIManager(),
                    h.Overlay,
                    publisher,
                    () => false,
                    () => "Search text",
                    "bottom");

                await step.ExecuteAsync(CancellationToken.None);

                Assert.AreEqual(0, publisher.Messages.Count);
                Assert.IsNull(h.Root.transform.Find("TutorialOverlayRoot"));
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async Task EmptyLazyText_DoesNotCreateOverlayOrPublishPause()
        {
            var h = new OverlayHarness("TutorialBlockingCalloutStepTests_Root");
            try
            {
                var publisher = new RecordingPublisher<SalesPauseRequested>();
                var step = new TutorialBlockingCalloutStep(
                    "empty_text",
                    new FakeUIManager(),
                    h.Overlay,
                    publisher,
                    () => true,
                    () => null,
                    "bottom");

                await step.ExecuteAsync(CancellationToken.None);

                Assert.AreEqual(0, publisher.Messages.Count);
                Assert.IsNull(h.Root.transform.Find("TutorialOverlayRoot"));
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async Task CancellationAfterPause_PublishesResume()
        {
            var h = new OverlayHarness("TutorialBlockingCalloutStepTests_Root");
            using var cts = new CancellationTokenSource();
            try
            {
                var publisher = new RecordingPublisher<SalesPauseRequested>();
                var step = new TutorialBlockingCalloutStep(
                    "cancel",
                    new FakeUIManager(),
                    h.Overlay,
                    publisher,
                    () => true,
                    () => "Search text",
                    "bottom");

                var run = step.ExecuteAsync(cts.Token);
                await UniTask.Yield(PlayerLoopTiming.Update);
                cts.Cancel();

                try
                {
                    await run;
                }
                catch (OperationCanceledException)
                {
                }

                Assert.AreEqual(2, publisher.Messages.Count);
                Assert.IsTrue(publisher.Messages[0].Paused);
                Assert.IsFalse(publisher.Messages[1].Paused);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async Task ConsecutiveBlockingCallouts_ReblockAndReplaceText()
        {
            var h = new OverlayHarness("TutorialBlockingCalloutStepTests_Root");
            try
            {
                var ui = new FakeUIManager();
                var publisher = new RecordingPublisher<SalesPauseRequested>();
                var first = new TutorialBlockingCalloutStep(
                    "first",
                    ui,
                    h.Overlay,
                    publisher,
                    () => true,
                    () => "text_1",
                    "bottom");
                var second = new TutorialBlockingCalloutStep(
                    "second",
                    ui,
                    h.Overlay,
                    publisher,
                    () => true,
                    () => "text_2",
                    "bottom");

                var firstRun = first.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);
                Assert.IsTrue(h.Blackout.gameObject.activeSelf);
                Assert.IsTrue(publisher.Messages[0].Paused);

                h.Blackout.OnPointerClick(null);
                await firstRun;
                Assert.IsFalse(h.Blackout.gameObject.activeSelf);
                Assert.IsTrue(h.TextPanel.gameObject.activeSelf);
                Assert.IsFalse(publisher.Messages[1].Paused);

                var secondRun = second.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);
                Assert.IsTrue(h.Blackout.gameObject.activeSelf, "Second blocking callout must dim again.");
                Assert.IsTrue(h.TextPanel.gameObject.activeSelf, "The text panel is reused and remains visible.");
                Assert.IsTrue(publisher.Messages[2].Paused);

                h.Blackout.OnPointerClick(null);
                await secondRun;
                Assert.IsFalse(h.Blackout.gameObject.activeSelf);
                Assert.IsTrue(h.TextPanel.gameObject.activeSelf);
                Assert.IsFalse(publisher.Messages[3].Paused);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async Task HideTextAfterTap_HidesPanelAndPublishesResume()
        {
            var h = new OverlayHarness("TutorialBlockingCalloutStepTests_Root");
            try
            {
                var publisher = new RecordingPublisher<SalesPauseRequested>();
                var step = new TutorialBlockingCalloutStep(
                    "hide_after_tap",
                    new FakeUIManager(),
                    h.Overlay,
                    publisher,
                    () => true,
                    () => "Sold text",
                    "bottom",
                    hideTextAfterTap: true);

                var run = step.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsTrue(h.Blackout.gameObject.activeSelf);
                Assert.IsTrue(h.TextPanel.gameObject.activeSelf);

                h.Blackout.OnPointerClick(null);
                await run;

                Assert.AreEqual(2, publisher.Messages.Count);
                Assert.IsFalse(publisher.Messages[1].Paused);
                Assert.IsFalse(h.Blackout.gameObject.activeSelf);
                Assert.IsFalse(h.TextPanel.gameObject.activeSelf);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async Task NoDimNoUiLock_ShowsTextWithoutBlackout_AndKeepsUiUnlocked()
        {
            var h = new OverlayHarness("TutorialBlockingCalloutStepTests_NoDim");
            using var cts = new CancellationTokenSource();
            try
            {
                var ui = new FakeUIManager();
                var publisher = new RecordingPublisher<SalesPauseRequested>();
                var step = new TutorialBlockingCalloutStep(
                    "no_dim",
                    ui,
                    h.Overlay,
                    publisher,
                    () => true,
                    () => "Chance text",
                    "bottom",
                    hideTextAfterTap: true,
                    dimBackground: false,
                    lockUi: false);

                var run = step.ExecuteAsync(cts.Token);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.AreEqual(1, publisher.Messages.Count);
                Assert.IsTrue(publisher.Messages[0].Paused);
                Assert.IsFalse(ui.HasManualLock);
                Assert.IsFalse(h.Blackout.gameObject.activeSelf);
                Assert.IsTrue(h.TextPanel.gameObject.activeSelf);

                cts.Cancel();
                try
                {
                    await run;
                }
                catch (OperationCanceledException)
                {
                }

                Assert.AreEqual(2, publisher.Messages.Count);
                Assert.IsFalse(publisher.Messages[1].Paused);
                Assert.IsFalse(h.TextPanel.gameObject.activeSelf);
            }
            finally
            {
                h.Dispose();
            }
        }

        private sealed class OverlayHarness : IDisposable
        {
            private readonly TutorialOverlaySettings _settings;
            private readonly GameObject _panelPrefab;

            public OverlayHarness(string rootName)
            {
                Root = new GameObject(rootName);
                _panelPrefab = new GameObject("TutorialTextPanelPrefab", typeof(RectTransform), typeof(TutorialTextPanelView));
                _settings = TutorialOverlaySettings.CreateDefault();
                typeof(TutorialOverlaySettings)
                    .GetField("_textPanelPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(_settings, _panelPrefab.GetComponent<TutorialTextPanelView>());
                Overlay = new TutorialOverlayController(new FakeCanvasRoot(Root.transform), _settings);
            }

            public GameObject Root { get; }
            public TutorialOverlayController Overlay { get; }
            public TutorialBlackoutView Blackout => Root.transform
                .Find("TutorialOverlayRoot/Blackout")
                .GetComponent<TutorialBlackoutView>();
            public TutorialTextPanelView TextPanel => Root.GetComponentInChildren<TutorialTextPanelView>(true);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(_panelPrefab);
                UnityEngine.Object.DestroyImmediate(_settings);
            }
        }

        private sealed class FakeCanvasRoot : IUICanvasRoot
        {
            public FakeCanvasRoot(Transform root)
            {
                HudRoot = root;
                WindowsRoot = root;
            }

            public Transform HudRoot { get; }
            public Transform WindowsRoot { get; }
            public GameObject Blocker => null;
            public MonoBehaviour TransitionAnimation => null;
        }

        private sealed class FakeUIManager : IUIManager
        {
            private readonly LockMonitor _locks = new();

            public event Action<IWindowController> WindowShown;
            public bool HasManualLock => _locks.HasAnyLock;

            public UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
                where T : class, IWindowController, new()
                => UniTask.FromResult<T>(null);

            public UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
                where T : class, IWindowController
                => UniTask.CompletedTask;

            public UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public IWindowController GetTopWindow(WindowLayer? layer = null) => null;
            public bool IsWindowShown<T>() where T : class, IWindowController => false;
            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Game.UI.Lock SetManualLock(object owner) => _locks.Acquire(owner);
        }

        private sealed class RecordingPublisher<T> : IPublisher<T>
        {
            public List<T> Messages { get; } = new();

            public void Publish(T message)
            {
                Messages.Add(message);
            }
        }
    }
}
