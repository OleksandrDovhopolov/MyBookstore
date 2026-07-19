using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.Content;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.TutorialUI;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialHighlightClickStepTests
    {
        [Test]
        public async System.Threading.Tasks.Task MissingTarget_AutoAdvances_WithoutCreatingOverlay()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_Missing");
            try
            {
                var step = new TutorialHighlightClickStep(
                    "missing",
                    h.Overlay,
                    new TutorialTargetRegistry(),
                    "missing.target",
                    "Text",
                    "bottom");

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex(@"\[Tutorial\] highlight target 'missing\.target' not found; auto-advancing\."));
                await step.ExecuteAsync(CancellationToken.None);

                Assert.IsNull(h.OverlayRoot);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task GateFalse_CompletesWithoutResolvingTargetOrCreatingOverlay()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_GateFalse");
            try
            {
                var step = new TutorialHighlightClickStep(
                    "gated",
                    h.Overlay,
                    new TutorialTargetRegistry(),
                    "missing.target",
                    "Text",
                    "bottom",
                    true,
                    () => false);

                await step.ExecuteAsync(CancellationToken.None);

                Assert.IsNull(h.OverlayRoot);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task LazyText_IsReadOnExecute_AndHitAreaClickCompletes()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_Click");
            try
            {
                var registry = new TutorialTargetRegistry();
                registry.Register("panel", h.Target);

                var text = "Initial";
                var readCount = 0;
                var step = new TutorialHighlightClickStep(
                    "click_panel",
                    h.Overlay,
                    registry,
                    "panel",
                    () =>
                    {
                        readCount++;
                        return text;
                    },
                    "bottom");

                text = "Updated";
                var run = step.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.AreEqual(1, readCount);
                Assert.IsTrue(h.HitArea.gameObject.activeSelf);

                h.HitArea.OnPointerClick(null);
                await run;

                Assert.IsFalse(h.HitArea.gameObject.activeSelf);
                Assert.IsFalse(h.Blackout.gameObject.activeSelf);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task Cancellation_Propagates_AndHidesHighlight()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_Cancel");
            using var cts = new CancellationTokenSource();
            try
            {
                var registry = new TutorialTargetRegistry();
                registry.Register("panel", h.Target);

                var step = new TutorialHighlightClickStep(
                    "cancel_panel",
                    h.Overlay,
                    registry,
                    "panel",
                    "Text",
                    "bottom");

                var run = step.ExecuteAsync(cts.Token);
                await UniTask.Yield(PlayerLoopTiming.Update);
                Assert.IsTrue(h.HitArea.gameObject.activeSelf);

                cts.Cancel();

                try
                {
                    await run;
                    Assert.Fail("Expected OperationCanceledException.");
                }
                catch (OperationCanceledException)
                {
                }

                Assert.IsFalse(h.HitArea.gameObject.activeSelf);
                Assert.IsFalse(h.Blackout.gameObject.activeSelf);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task ButtonTarget_UsesButtonClick_AndKeepsHitAreaHidden()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_Button");
            try
            {
                var button = h.Target.gameObject.AddComponent<Button>();
                var run = h.Overlay.HighlightAndWaitClickAsync(
                    h.Target,
                    "Text",
                    "bottom",
                    true,
                    CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(h.HitArea.gameObject.activeSelf);

                button.onClick.Invoke();
                await run;

                Assert.IsFalse(h.HitArea.gameObject.activeSelf);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task PointerPlacementLeft_PositionsPointerLeftOfTarget()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_PointerLeft");
            try
            {
                var run = h.Overlay.HighlightAndWaitClickAsync(
                    h.Target,
                    "Text",
                    "bottom",
                    true,
                    TutorialPointerPlacement.Left,
                    CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                InvokeLateUpdate(h.Pointer);

                Assert.IsTrue(ScreenRectUtility.TryGetLocalRect(
                    h.Target,
                    (RectTransform)h.OverlayRoot,
                    out var targetRect));
                var pointerRt = (RectTransform)h.Pointer.transform;

                Assert.Less(pointerRt.anchoredPosition.x, targetRect.xMin);
                Assert.AreEqual(targetRect.center.y, pointerRt.anchoredPosition.y, 0.5f);

                h.HitArea.OnPointerClick(null);
                await run;
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task PauseSales_PublishesPauseAroundHighlight_AndStillCompletesFromHitAreaClick()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_PauseSales");
            try
            {
                var registry = new TutorialTargetRegistry();
                registry.Register("panel", h.Target);
                var publisher = new RecordingPublisher<SalesPauseRequested>();

                var step = new TutorialHighlightClickStep(
                    "pause_panel",
                    h.Overlay,
                    registry,
                    "panel",
                    "Text",
                    "bottom",
                    true,
                    null,
                    TutorialPointerPlacement.Left,
                    publisher,
                    pauseSales: true);

                var run = step.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.AreEqual(1, publisher.Messages.Count);
                Assert.IsTrue(publisher.Messages[0].Paused);
                Assert.IsTrue(h.HitArea.gameObject.activeSelf);

                h.HitArea.OnPointerClick(null);
                await run;

                Assert.AreEqual(2, publisher.Messages.Count);
                Assert.IsFalse(publisher.Messages[1].Paused);
                Assert.IsFalse(h.HitArea.gameObject.activeSelf);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task Hide_HidesActiveHitArea()
        {
            var h = new OverlayHarness("TutorialHighlightClickStepTests_Hide");
            try
            {
                var run = h.Overlay.HighlightAndWaitClickAsync(
                    h.Target,
                    "Text",
                    "bottom",
                    true,
                    CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsTrue(h.HitArea.gameObject.activeSelf);

                h.Overlay.Hide();
                Assert.IsFalse(h.HitArea.gameObject.activeSelf);

                h.HitArea.OnPointerClick(null);
                await run;
            }
            finally
            {
                h.Dispose();
            }
        }

        private static void InvokeLateUpdate(TutorialPointerView pointer)
            => typeof(TutorialPointerView)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pointer, null);

        private sealed class OverlayHarness : IDisposable
        {
            private readonly TutorialOverlaySettings _settings;

            public OverlayHarness(string rootName)
            {
                Root = new GameObject(rootName, typeof(RectTransform));
                var rootRt = (RectTransform)Root.transform;
                rootRt.sizeDelta = new Vector2(800f, 600f);

                var targetGo = new GameObject("Target", typeof(RectTransform));
                Target = (RectTransform)targetGo.transform;
                Target.SetParent(Root.transform, false);
                Target.anchorMin = Vector2.zero;
                Target.anchorMax = Vector2.zero;
                Target.pivot = Vector2.zero;
                Target.anchoredPosition = new Vector2(100f, 100f);
                Target.sizeDelta = new Vector2(120f, 64f);

                _settings = TutorialOverlaySettings.CreateDefault();
                Overlay = new TutorialOverlayController(new FakeCanvasRoot(Root.transform), _settings);
            }

            public GameObject Root { get; }
            public RectTransform Target { get; }
            public TutorialOverlayController Overlay { get; }
            public Transform OverlayRoot => Root.transform.Find("TutorialOverlayRoot");
            public TutorialBlackoutView Blackout => OverlayRoot.GetComponentInChildren<TutorialBlackoutView>(true);
            public TutorialHitAreaView HitArea => OverlayRoot.GetComponentInChildren<TutorialHitAreaView>(true);
            public TutorialPointerView Pointer => OverlayRoot.GetComponentInChildren<TutorialPointerView>(true);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
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
