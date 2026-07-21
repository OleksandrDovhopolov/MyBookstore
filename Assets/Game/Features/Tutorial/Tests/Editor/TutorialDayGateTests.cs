using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Game.Tutorial.API;
using Game.Tutorial.Services;
using Game.UI;
using MessagePipe;
using NUnit.Framework;
using Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialDayGateTests
    {
        [Test]
        public async Task Day1_LocationLoaded_StartsDayOneSequence()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CurrentDay = 1;
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(dayProgress, gameFlow);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                gameFlow.RaiseLocationLoaded(true);

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("tutorial_day_1", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Day1_DayOneCompleted_ReEnterLocation_StartsNothing()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CurrentDay = 1;
            var save = new FakeSaveService(new TutorialSaveState
            {
                CompletedSequenceIds = new List<string> { "tutorial_day_1" }
            });
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(dayProgress, gameFlow, save);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                gameFlow.RaiseLocationLoaded(true);

                Assert.IsFalse(service.IsRunning);
                Assert.IsNull(service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Day2_LocationLoaded_StartsNothing()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CurrentDay = 2;
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(dayProgress, gameFlow);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                gameFlow.RaiseLocationLoaded(true);

                Assert.IsFalse(service.IsRunning);
                Assert.IsNull(service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Day2_DayOneNotCompleted_StartsNothing()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CurrentDay = 2;
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(dayProgress, gameFlow);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                gameFlow.RaiseLocationLoaded(true);

                Assert.IsFalse(service.IsRunning);
                Assert.IsNull(service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task EligibleHubSequence_StartsFromDifferentTrigger()
        {
            var dayProgress = new FakeDayProgress();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var hub = new FakeSequence
            {
                Id = "tutorial_hub",
                Priority = 30,
                Context = TutorialContext.Hub,
                Trigger = TutorialTrigger.HubReady,
                Steps = new ITutorialStep[] { new BlockingStep("hold") }
            };
            var service = BuildService(dayProgress, gameFlow, sequences: new ITutorialSequence[] { hub });
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("tutorial_hub", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task RequestReevaluation_StartsEligibleSequence()
        {
            var dayProgress = new FakeDayProgress();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var sequence = new FakeSequence
            {
                Id = "domain_driven",
                Priority = 10,
                Context = TutorialContext.Hub,
                Steps = new ITutorialStep[] { new BlockingStep("hold") }
            };
            var service = BuildService(dayProgress, gameFlow, sequences: new ITutorialSequence[] { sequence });
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                ((ITutorialReevaluationGate)service).RequestReevaluation();

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("domain_driven", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Trigger_WhileFocusableWindowOpen_DoesNotStartUntilWindowHidden()
        {
            var dayProgress = new FakeDayProgress();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var ui = new FakeUIManager { TopWindow = new FakeWindowController() };
            var sequence = new FakeSequence
            {
                Id = "blocked_by_window",
                Priority = 10,
                Context = TutorialContext.Hub,
                Steps = new ITutorialStep[] { new BlockingStep("hold") }
            };
            var service = BuildService(
                dayProgress,
                gameFlow,
                sequences: new ITutorialSequence[] { sequence },
                ui: ui);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);

                Assert.IsFalse(service.IsRunning);

                ui.TopWindow = null;
                ui.RaiseWindowHidden();

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("blocked_by_window", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task HubTutorial_PhaseMorningWhileResultsWindowOpen_StartsAfterWindowHidden()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Results;
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var ui = new FakeUIManager { TopWindow = new FakeWindowController() };
            var hub = new FakeSequence
            {
                Id = "tutorial_hub",
                Priority = 30,
                Context = TutorialContext.Hub,
                Trigger = TutorialTrigger.HubReady,
                IsEligibleFunc = () => dayProgress.Current.CompletedDays.Contains(1)
                                     && dayProgress.Current.CurrentPhase == DayPhase.Morning,
                Steps = new ITutorialStep[] { new BlockingStep("hold") }
            };
            var service = BuildService(
                dayProgress,
                gameFlow,
                sequences: new ITutorialSequence[] { hub },
                ui: ui);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);

                Assert.IsFalse(service.IsRunning);

                ui.TopWindow = null;
                ui.RaiseWindowHidden();

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("tutorial_hub", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Day1_LocationLoaded_StartsWhenOnlyHudIsPresent()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CurrentDay = 1;
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(dayProgress, gameFlow, ui: new FakeUIManager { TopWindow = null });
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                gameFlow.RaiseLocationLoaded(true);

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("tutorial_day_1", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task CompletedSequence_RescansAndStartsNextEligibleSequence()
        {
            var dayProgress = new FakeDayProgress();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var bEligible = false;
            var a = new FakeSequence
            {
                Id = "a",
                Priority = 10,
                Context = TutorialContext.Hub,
                Steps = new ITutorialStep[] { new CallbackStep("finish_a", () => bEligible = true) }
            };
            var b = new FakeSequence
            {
                Id = "b",
                Priority = 20,
                Context = TutorialContext.Hub,
                IsEligibleFunc = () => bEligible,
                Steps = new ITutorialStep[] { new BlockingStep("hold_b") }
            };
            var service = BuildService(dayProgress, gameFlow, sequences: new ITutorialSequence[] { a, b });
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("b", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task FailedSequence_DoesNotImmediatelyRescanAndRestart()
        {
            var dayProgress = new FakeDayProgress();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var started = new RecordingPublisher<TutorialSequenceStarted>();
            var failing = new FakeSequence
            {
                Id = "failing",
                Priority = 10,
                Context = TutorialContext.Hub,
                Steps = new ITutorialStep[] { new FailingStep("fail") }
            };
            var service = BuildService(
                dayProgress,
                gameFlow,
                sequences: new ITutorialSequence[] { failing },
                startedPub: started);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                LogAssert.Expect(LogType.Error, new Regex("\\[Tutorial\\] sequence 'failing' failed:.*boom"));
                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(service.IsRunning);
                Assert.AreEqual(1, started.Messages.Count);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task CompletedSequence_DoesNotStartAgainOnLaterTriggers()
        {
            var dayProgress = new FakeDayProgress();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var started = new RecordingPublisher<TutorialSequenceStarted>();
            var sequence = new FakeSequence
            {
                Id = "one_way",
                Priority = 10,
                Context = TutorialContext.Hub,
                Steps = new ITutorialStep[] { new CallbackStep("done", null) }
            };
            var service = BuildService(
                dayProgress,
                gameFlow,
                sequences: new ITutorialSequence[] { sequence },
                startedPub: started);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.AreEqual(1, started.Messages.Count);
                Assert.IsTrue(service.IsSequenceCompleted("one_way"));
            }
            finally
            {
                service.Dispose();
            }
        }

        private static TutorialService BuildService(
            FakeDayProgress dayProgress,
            FakeGameFlow gameFlow,
            FakeSaveService save = null,
            IReadOnlyList<ITutorialSequence> sequences = null,
            IPublisher<TutorialSequenceStarted> startedPub = null,
            IUIManager ui = null)
        {
            var dayOne = new FakeSequence
            {
                Id = "tutorial_day_1",
                Priority = 10,
                IsEligibleFunc = () => dayProgress.Current.CurrentDay == 1
            };
            return new TutorialService(
                save ?? new FakeSaveService(),
                sequences ?? new ITutorialSequence[] { dayOne },
                hubReadySub: null,
                startedPub: startedPub,
                stepPub: null,
                completedPub: null,
                dayProgress: dayProgress,
                gameFlow: gameFlow,
                ui: ui);
        }

        private sealed class FakeSequence : ITutorialSequence
        {
            public string Id { get; set; }
            public int Priority { get; set; }
            public TutorialContext Context { get; set; } = TutorialContext.Location;
            public TutorialTrigger Trigger { get; set; } = TutorialTrigger.LocationLoaded;
            public string TriggerParam { get; set; }
            public TutorialResumePolicy ResumePolicy { get; set; } = TutorialResumePolicy.Restart;
            public Func<bool> IsEligibleFunc { get; set; } = () => true;
            public IReadOnlyList<ITutorialStep> Steps { get; set; } = new ITutorialStep[] { new BlockingStep("hold") };

            public bool IsEligible() => IsEligibleFunc();
            public IReadOnlyList<ITutorialStep> GetSteps() => Steps;
            public void OnRunStarted() { }
            public void OnRunEnded() { }
        }

        private sealed class BlockingStep : ITutorialStep
        {
            public BlockingStep(string id) => Id = id;

            public string Id { get; }

            public async UniTask ExecuteAsync(CancellationToken ct)
            {
                while (true)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private sealed class CallbackStep : ITutorialStep
        {
            private readonly Action _callback;

            public CallbackStep(string id, Action callback)
            {
                Id = id;
                _callback = callback;
            }

            public string Id { get; }

            public UniTask ExecuteAsync(CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                _callback?.Invoke();
                return UniTask.CompletedTask;
            }
        }

        private sealed class FailingStep : ITutorialStep
        {
            public FailingStep(string id) => Id = id;
            public string Id { get; }
            public UniTask ExecuteAsync(CancellationToken ct) => throw new InvalidOperationException("boom");
        }

        private sealed class RecordingPublisher<T> : IPublisher<T>
        {
            public List<T> Messages { get; } = new();
            public void Publish(T message) => Messages.Add(message);
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged;

            public DayProgressState Current { get; } = new();

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

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _modules = new();

            public FakeSaveService(TutorialSaveState initialState = null)
            {
                if (initialState != null)
                    _modules[TutorialSaveKeys.State] = initialState;
            }

            public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular) => UniTask.CompletedTask;

            public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
                => UniTask.FromResult(_modules.TryGetValue(moduleKey, out var value) ? value as T : null);

            public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
            {
                _modules[moduleKey] = value;
                return UniTask.CompletedTask;
            }

            public void MarkDirty() { }
            public void RegisterHook(ISaveHook hook) { }
            public IDisposable BlockAutosave() => new NoopLease();
            public void Dispose() { }

            private sealed class NoopLease : IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class FakeGameFlow : IGameFlowService
        {
            public bool IsTransitioning { get; set; }
            public bool IsLocationLoaded { get; set; }

            public event Action<bool> LocationLoadedChanged;

            public void RegisterHubRoot(GameObject hubRoot) { }
            public UniTask EnterLocationAsync(string locationId, CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask ReturnToHubAsync(CancellationToken ct = default) => UniTask.CompletedTask;

            public void RaiseLocationLoaded(bool loaded)
            {
                IsLocationLoaded = loaded;
                LocationLoadedChanged?.Invoke(loaded);
            }
        }

        private sealed class FakeUIManager : IUIManager
        {
            private readonly LockMonitor _locks = new();

            public event Action<IWindowController> WindowShown;
            public event Action<IWindowController> WindowHidden;
            public IWindowController TopWindow { get; set; }

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

            public IWindowController GetTopWindow(WindowLayer? layer = null) => TopWindow;
            public bool IsWindowShown<T>() where T : class, IWindowController => false;
            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Game.UI.Lock SetManualLock(object owner) => _locks.Acquire(owner);

            public void RaiseWindowHidden() => WindowHidden?.Invoke(new FakeWindowController());
        }

        private sealed class FakeWindowController : IWindowController
        {
            public WindowAttribute Attribute { get; } = new("Fake", WindowType.Popup);
            public WindowArgs Arguments => null;
            public IWindow View => null;
            public bool IsShown => true;
            public bool IsCloseBlocked => false;
            public event Action<IWindowController> Closed;
            public void Configure(WindowView view, WindowAttribute attribute) { }
            public void ApplyArguments(WindowArgs args) { }
            public UniTask ShowAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask HideAsync(bool isClosed, CancellationToken ct) => UniTask.CompletedTask;
            public void SetHudVisible(bool visible) { }
            public void Dispose() { }
        }
    }
}
