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
using Infrastructure.TutorialUI;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialDayTwoTests
    {
        [Test]
        public async System.Threading.Tasks.Task AwaitPassiveFail_CompletesForAnyCharacter()
        {
            var h = new Harness();
            try
            {
                h.Sequence.OnRunStarted();
                var step = h.Sequence.GetSteps()[0];

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
        public async System.Threading.Tasks.Task AwaitPassiveFail_CompletesWhenResultsShown()
        {
            var h = new Harness();
            try
            {
                h.Sequence.OnRunStarted();
                h.Ui.ResultsShown = true;

                await h.Sequence.GetSteps()[0].ExecuteAsync(CancellationToken.None);

                Assert.Pass();
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task OnRunEnded_UnsubscribesAndResetsLatch_AndPublishesResume()
        {
            var h = new Harness();
            try
            {
                h.Sequence.OnRunStarted();
                Assert.AreEqual(1, h.FailSubscriber.HandlerCount);

                h.FailSubscriber.Publish(new SalesPassivePurchaseFailed("customer_2", null, "Travel"));
                h.Sequence.OnRunEnded();

                Assert.AreEqual(1, h.PausePublisher.Messages.Count);
                Assert.IsFalse(h.PausePublisher.Messages[0].Paused);
                Assert.AreEqual(0, h.FailSubscriber.HandlerCount);

                h.FailSubscriber.Publish(new SalesPassivePurchaseFailed("customer_after_end", null, "Travel"));
                h.Sequence.OnRunStarted();
                Assert.AreEqual(1, h.FailSubscriber.HandlerCount);

                var run = h.Sequence.GetSteps()[0].ExecuteAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);
                h.FailSubscriber.Publish(new SalesPassivePurchaseFailed("customer_after_restart", null, "Travel"));
                await run;
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public void GetSteps_UsesExpectedDayTwoFlow()
        {
            var h = new Harness();
            try
            {
                var steps = h.Sequence.GetSteps();

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "await_passive_fail",
                        "verify_passive_fail",
                        "text_1",
                        "text_2",
                        "text_3",
                        "click_genre_panel",
                        "text_4",
                    },
                    StepIds(steps));
                Assert.IsInstanceOf<TutorialAwaitFactStep>(steps[0]);
                Assert.IsInstanceOf<TutorialAssertStep>(steps[1]);
                Assert.IsInstanceOf<TutorialBlockingCalloutStep>(steps[2]);
                Assert.IsInstanceOf<TutorialHighlightClickStep>(steps[5]);
                Assert.IsInstanceOf<TutorialBlockingCalloutStep>(steps[6]);
                Assert.AreEqual(
                    TutorialPointerPlacement.Left,
                    ReadPointerPlacement((TutorialHighlightClickStep)steps[5]));
                Assert.IsTrue(ReadPauseSales((TutorialHighlightClickStep)steps[5]));
                Assert.IsTrue(ReadHideTextAfterTap((TutorialBlockingCalloutStep)steps[6]));
            }
            finally
            {
                h.Dispose();
            }
        }

        [Test]
        public void IsEligible_OnlyOnDayTwo()
        {
            var h = new Harness();
            try
            {
                h.DayProgress.Current.CurrentDay = 1;
                Assert.IsFalse(h.Sequence.IsEligible());

                h.DayProgress.Current.CurrentDay = 2;
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

        private static bool ReadHideTextAfterTap(TutorialBlockingCalloutStep step)
            => (bool)typeof(TutorialBlockingCalloutStep)
                .GetField("_hideTextAfterTap", BindingFlags.Instance | BindingFlags.NonPublic)
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
                Root = new GameObject("TutorialDayTwoTests_Root");
                _settings = TutorialOverlaySettings.CreateDefault();
                Overlay = new TutorialOverlayController(new FakeCanvasRoot(Root.transform), _settings);
                Sequence = new TutorialDayTwo(
                    Overlay,
                    Ui,
                    DayProgress,
                    Targets,
                    PausePublisher,
                    FailSubscriber,
                    analytics: null);
            }

            public GameObject Root { get; }
            public FakeUIManager Ui { get; } = new();
            public FakeDayProgress DayProgress { get; } = new();
            public TutorialTargetRegistry Targets { get; } = new();
            public RecordingPublisher<SalesPauseRequested> PausePublisher { get; } = new();
            public RecordingSubscriber<SalesPassivePurchaseFailed> FailSubscriber { get; } = new();
            public TutorialOverlayController Overlay { get; }
            public TutorialDayTwo Sequence { get; }

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
            public bool ResultsShown { get; set; }

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
            public bool IsWindowShown<T>() where T : class, IWindowController
                => typeof(T) == typeof(ResultsWindow) && ResultsShown;
            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Game.UI.Lock SetManualLock(object owner) => new LockMonitor().Acquire(owner);
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged;

            public DayProgressState Current { get; } = new() { CurrentDay = 2 };
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
                return new Subscription(this, handler);
            }

            public void Publish(T message)
            {
                for (var i = 0; i < _handlers.Count; i++)
                    _handlers[i]?.Handle(message);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly RecordingSubscriber<T> _owner;
                private readonly IMessageHandler<T> _handler;
                private bool _disposed;

                public Subscription(RecordingSubscriber<T> owner, IMessageHandler<T> handler)
                {
                    _owner = owner;
                    _handler = handler;
                }

                public void Dispose()
                {
                    if (_disposed) return;
                    _disposed = true;
                    _owner._handlers.Remove(_handler);
                }
            }
        }
    }
}
