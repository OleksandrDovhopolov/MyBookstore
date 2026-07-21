using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Content;
using Game.Tutorial.Presentation;
using Game.UI;
using Game.UI.ContentWidget;
using Infrastructure.TutorialUI;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialDayOneTests
    {
        [Test]
        public async System.Threading.Tasks.Task AwaitPostEddiPassiveFail_CompletesForNullCharacter()
        {
            var h = new Harness();
            try
            {
                h.Sequence.OnRunStarted();
                var step = StepById(h.Sequence.GetSteps(), "await_passive_fail");

                var run = step.ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                h.FailSubscriber.Publish(new SalesPassivePurchaseFailed("customer_2", null, "Travel"));

                await run;
                Assert.Pass();
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task AwaitPostEddiPassiveFail_IgnoresEddiFailure()
        {
            var h = new Harness();
            using var cts = new CancellationTokenSource();
            try
            {
                h.Sequence.OnRunStarted();
                var task = StepById(h.Sequence.GetSteps(), "await_passive_fail").ExecuteAsync(cts.Token).AsTask();
                await UniTask.Yield(PlayerLoopTiming.Update);

                h.FailSubscriber.Publish(new SalesPassivePurchaseFailed("eddi_customer", "eddi", "Travel"));
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(task.IsCompleted);
                cts.Cancel();

                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    Assert.Pass();
                }
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task AwaitPostEddiPassiveFail_CompletesWhenResultsShown()
        {
            var h = new Harness();
            try
            {
                h.Sequence.OnRunStarted();
                h.Ui.ResultsShown = true;

                await StepById(h.Sequence.GetSteps(), "await_passive_fail").ExecuteAsync(CancellationToken.None);

                Assert.Pass();
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public void OnRunEnded_UnsubscribesAndResetsLatch_AndPublishesResume()
        {
            var h = new Harness();
            try
            {
                h.Sequence.OnRunStarted();
                Assert.AreEqual(1, h.PhaseSubscriber.HandlerCount);
                Assert.AreEqual(1, h.SaleSubscriber.HandlerCount);
                Assert.AreEqual(1, h.FailSubscriber.HandlerCount);

                h.FailSubscriber.Publish(new SalesPassivePurchaseFailed("customer_2", null, "Travel"));
                h.Sequence.OnRunEnded();

                Assert.AreEqual(1, h.PausePublisher.Messages.Count);
                Assert.IsFalse(h.PausePublisher.Messages[0].Paused);
                Assert.AreEqual(0, h.PhaseSubscriber.HandlerCount);
                Assert.AreEqual(0, h.SaleSubscriber.HandlerCount);
                Assert.AreEqual(0, h.FailSubscriber.HandlerCount);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public void OnRunEnded_HidesShownContentWidget()
        {
            var h = new Harness();
            try
            {
                h.Ui.ContentWidgetShown = true;

                h.Sequence.OnRunEnded();

                Assert.AreEqual(1, h.Ui.HideContentWidgetCount);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public void GetSteps_UsesMergedDayOneFlow()
        {
            var h = new Harness();
            try
            {
                var steps = h.Sequence.GetSteps();

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "checkpoint_eddi_intro_start",
                        "await_eddi_dialogue_complete",
                        "callout_search",
                        "await_eddi_sale",
                        "callout_sold",
                        "await_eddi_fail",
                        "callout_failed",
                        "await_eddi_left",
                        "hide_eddi_callout",
                        "checkpoint_eddi_intro_end",
                        "verify_eddi_participated",
                        "await_passive_fail",
                        "verify_passive_fail",
                        "text_1",
                        "text_2",
                        "text_3",
                        "checkpoint_sale_chance_start",
                        "click_genre_panel",
                        "text_4",
                        "hide_sale_chance_widget",
                        "wait_results_window",
                        "wrap_up",
                        "wait_results_closed",
                    },
                    StepIds(steps));
                Assert.IsInstanceOf<TutorialActionStep>(StepById(steps, "checkpoint_eddi_intro_start"));
                Assert.IsInstanceOf<TutorialAwaitFactStep>(StepById(steps, "await_passive_fail"));
                Assert.IsInstanceOf<TutorialAssertStep>(StepById(steps, "verify_passive_fail"));
                Assert.IsInstanceOf<TutorialBlockingCalloutStep>(StepById(steps, "text_1"));
                Assert.IsInstanceOf<TutorialActionStep>(StepById(steps, "checkpoint_sale_chance_start"));
                Assert.IsInstanceOf<TutorialHighlightClickStep>(StepById(steps, "click_genre_panel"));
                Assert.IsInstanceOf<TutorialBlockingCalloutStep>(StepById(steps, "text_4"));
                Assert.IsInstanceOf<TutorialHideWindowStep<ContentWidgetController>>(StepById(steps, "hide_sale_chance_widget"));
                var highlightStep = (TutorialHighlightClickStep)StepById(steps, "click_genre_panel");
                var text4Step = (TutorialBlockingCalloutStep)StepById(steps, "text_4");
                Assert.AreEqual(
                    TutorialPointerPlacement.Left,
                    ReadPointerPlacement(highlightStep));
                Assert.IsTrue(ReadPauseSales(highlightStep));
                Assert.IsTrue(ReadHideTextAfterTap(text4Step));
                Assert.IsFalse(ReadDimBackground(text4Step));
                Assert.IsFalse(ReadLockUi(text4Step));
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task HideWindowStep_HidesTypedWindowWhenShown()
        {
            var h = new Harness();
            try
            {
                h.Ui.ContentWidgetShown = true;
                var step = StepById(h.Sequence.GetSteps(), "hide_sale_chance_widget");

                await step.ExecuteAsync(CancellationToken.None);

                Assert.AreEqual(1, h.Ui.HideContentWidgetCount);
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public void IsEligible_OnlyOnDayOne()
        {
            var h = new Harness();
            try
            {
                h.DayProgress.Current.CurrentDay = 2;
                Assert.IsFalse(h.Sequence.IsEligible());

                h.DayProgress.Current.CurrentDay = 1;
                Assert.IsTrue(h.Sequence.IsEligible());
            }
            finally
            {
                h.Dispose();
            }
        }

        private static string[] StepIds(IReadOnlyList<ITutorialStep> steps)
        {
            var ids = new string[steps.Count];
            for (var i = 0; i < steps.Count; i++)
                ids[i] = steps[i].Id;
            return ids;
        }

        private static ITutorialStep StepById(IReadOnlyList<ITutorialStep> steps, string id)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Id == id)
                    return steps[i];
            }

            Assert.Fail($"Step '{id}' not found.");
            return null;
        }

        private static bool ReadHideTextAfterTap(TutorialBlockingCalloutStep step)
            => (bool)typeof(TutorialBlockingCalloutStep)
                .GetField("_hideTextAfterTap", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(step);

        private static bool ReadDimBackground(TutorialBlockingCalloutStep step)
            => (bool)typeof(TutorialBlockingCalloutStep)
                .GetField("_dimBackground", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(step);

        private static bool ReadLockUi(TutorialBlockingCalloutStep step)
            => (bool)typeof(TutorialBlockingCalloutStep)
                .GetField("_lockUi", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(step);

        private static TutorialPointerPlacement ReadPointerPlacement(TutorialHighlightClickStep step)
            => (TutorialPointerPlacement)typeof(TutorialHighlightClickStep)
                .GetField("_pointerPlacement", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(step);

        private static bool ReadPauseSales(TutorialHighlightClickStep step)
            => (bool)typeof(TutorialHighlightClickStep)
                .GetField("_pauseSales", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(step);

        private sealed class Harness : IDisposable
        {
            private readonly TutorialOverlaySettings _settings;

            public Harness()
            {
                Root = new GameObject("TutorialDayOneTests_Root");
                _settings = TutorialOverlaySettings.CreateDefault();
                Overlay = new TutorialOverlayController(new FakeCanvasRoot(Root.transform), _settings);
                Sequence = new TutorialDayOne(
                    Overlay,
                    Ui,
                    DayProgress,
                    Targets,
                    PhaseSubscriber,
                    SaleSubscriber,
                    FailSubscriber,
                    PausePublisher,
                    analytics: null);
            }

            public GameObject Root { get; }
            public FakeUIManager Ui { get; } = new();
            public FakeDayProgress DayProgress { get; } = new();
            public TutorialTargetRegistry Targets { get; } = new();
            public RecordingSubscriber<SalesCustomerPhaseChanged> PhaseSubscriber { get; } = new();
            public RecordingSubscriber<SalesPassiveSaleHappened> SaleSubscriber { get; } = new();
            public RecordingSubscriber<SalesPassivePurchaseFailed> FailSubscriber { get; } = new();
            public RecordingPublisher<SalesPauseRequested> PausePublisher { get; } = new();
            public TutorialOverlayController Overlay { get; }
            public TutorialDayOne Sequence { get; }

            public void Dispose()
            {
                Sequence.OnRunEnded();
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

        private sealed class FakeUIManager : IUIManager
        {
            public event Action<IWindowController> WindowShown;
            public event Action<IWindowController> WindowHidden;
            public bool ResultsShown { get; set; }
            public bool ContentWidgetShown { get; set; }
            public int HideContentWidgetCount { get; private set; }

            public UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
                where T : class, IWindowController, new()
                => UniTask.FromResult<T>(null);

            public UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
                where T : class, IWindowController
            {
                if (typeof(T) == typeof(ContentWidgetController))
                {
                    HideContentWidgetCount++;
                    ContentWidgetShown = false;
                }

                return UniTask.CompletedTask;
            }

            public UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public IWindowController GetTopWindow(WindowLayer? layer = null) => null;
            public bool IsWindowShown<T>() where T : class, IWindowController
                => (typeof(T) == typeof(ResultsWindow) && ResultsShown)
                   || (typeof(T) == typeof(ContentWidgetController) && ContentWidgetShown);
            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Game.UI.Lock SetManualLock(object owner) => new LockMonitor().Acquire(owner);
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged;

            public DayProgressState Current { get; } = new() { CurrentDay = 1 };
            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);
            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
            {
                Current.CurrentPhase = phase;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AdvanceToNextDayAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class RecordingPublisher<T> : IPublisher<T>
        {
            public List<T> Messages { get; } = new();

            public void Publish(T message)
            {
                Messages.Add(message);
            }
        }

        private sealed class RecordingSubscriber<T> : ISubscriber<T>
        {
            private readonly List<IMessageHandler<T>> _handlers = new();
            public int HandlerCount => _handlers.Count;

            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                _handlers.Add(handler);
                return new Subscription(_handlers, handler);
            }

            public void Publish(T message)
            {
                var snapshot = _handlers.ToArray();
                for (var i = 0; i < snapshot.Length; i++)
                    snapshot[i].Handle(message);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly List<IMessageHandler<T>> _handlers;
                private IMessageHandler<T> _handler;

                public Subscription(List<IMessageHandler<T>> handlers, IMessageHandler<T> handler)
                {
                    _handlers = handlers;
                    _handler = handler;
                }

                public void Dispose()
                {
                    if (_handler == null) return;
                    _handlers.Remove(_handler);
                    _handler = null;
                }
            }
        }
    }
}
