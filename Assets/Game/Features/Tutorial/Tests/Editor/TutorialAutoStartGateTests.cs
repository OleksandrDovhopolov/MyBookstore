using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Game.Tutorial.API;
using Game.Tutorial.Services;
using MessagePipe;
using NUnit.Framework;
using Save;
using UnityEngine;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialAutoStartGateTests
    {
        [Test]
        public async Task BlockedLocationLoaded_DoesNotStartImmediately()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(gate, gameFlow, autoStart: true);
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
        public async Task Release_ReplaysDeferredLocationLoaded()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(gate, gameFlow, autoStart: true);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);

                gate.Release();

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual(TutorialSequenceIds.DayOne, service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Release_RunsSingleScanAndStartsHighestPriorityEligibleSequence()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(
                gate,
                gameFlow,
                autoStart: true,
                sequences: new ITutorialSequence[]
                {
                    new FakeSequence { Id = "later", Priority = 20 },
                    new FakeSequence { Id = "earlier", Priority = 10 }
                });
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);

                gate.Release();

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("earlier", service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task AutoStartFalse_DoesNotReplayDeferredTrigger()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(gate, gameFlow, autoStart: false);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);

                gate.Release();

                Assert.IsFalse(service.IsRunning);
                Assert.IsNull(service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task Release_ReplaysDeferredHubTrigger()
        {
            var gate = new TutorialAutoStartGate();
            gate.Block();

            var dayProgress = new FakeDayProgress();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = false };
            var service = BuildService(
                gate,
                gameFlow,
                autoStart: true,
                dayProgress: dayProgress,
                sequence: new FakeSequence
                {
                    Id = TutorialSequenceIds.Hub,
                    Context = TutorialContext.Hub,
                    Trigger = TutorialTrigger.PhaseChanged
                });
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);

                await dayProgress.SetPhaseAsync(DayPhase.Morning, CancellationToken.None);

                Assert.IsFalse(service.IsRunning);
                Assert.IsNull(service.ActiveSequenceId);

                gate.Release();

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual(TutorialSequenceIds.Hub, service.ActiveSequenceId);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task ResumeFromStep_UsesSavedStepIdBeforeIndex()
        {
            var save = new FakeSaveService(new TutorialSaveState
            {
                ActiveSequenceId = TutorialSequenceIds.DayOne,
                NextStepId = "second",
                NextStepIndex = 0,
                CompletedSequenceIds = new List<string>()
            });
            var sequence = new FakeSequence
            {
                ResumePolicy = TutorialResumePolicy.FromStep,
                Steps = new ITutorialStep[]
                {
                    new BlockingStep("first"),
                    new BlockingStep("second")
                }
            };
            var stepPub = new RecordingPublisher<TutorialStepChanged>();
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(
                gate: null,
                gameFlow: gameFlow,
                autoStart: true,
                save: save,
                sequence: sequence,
                stepPub: stepPub);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsTrue(service.IsRunning);
                Assert.AreEqual("second", stepPub.Last.StepId);
                Assert.AreEqual(1, stepPub.Last.StepIndex);
            }
            finally
            {
                service.Dispose();
            }
        }

        private static TutorialService BuildService(
            TutorialAutoStartGate gate,
            FakeGameFlow gameFlow,
            bool autoStart,
            FakeSaveService save = null,
            ITutorialSequence sequence = null,
            IReadOnlyList<ITutorialSequence> sequences = null,
            IDayProgressService dayProgress = null,
            IPublisher<TutorialStepChanged> stepPub = null)
            => new(
                save ?? new FakeSaveService(),
                sequences ?? new[] { sequence ?? new FakeSequence() },
                hubReadySub: null,
                startedPub: null,
                stepPub: stepPub,
                completedPub: null,
                dayProgress: dayProgress,
                gameFlow: gameFlow,
                autoStartGate: gate,
                autoStart: autoStart);

        private sealed class FakeSequence : ITutorialSequence
        {
            public string Id { get; set; } = TutorialSequenceIds.DayOne;
            public int Priority { get; set; } = 10;
            public TutorialContext Context { get; set; } = TutorialContext.Location;
            public TutorialTrigger Trigger { get; set; } = TutorialTrigger.LocationLoaded;
            public string TriggerParam { get; set; }
            public TutorialResumePolicy ResumePolicy { get; set; } = TutorialResumePolicy.Restart;
            public bool Eligible { get; set; } = true;
            public IReadOnlyList<ITutorialStep> Steps { get; set; } = new ITutorialStep[] { new BlockingStep("hold") };

            public bool IsEligible() => Eligible;
            public IReadOnlyList<ITutorialStep> GetSteps() => Steps;
            public void OnRunStarted() { }
            public void OnRunEnded() { }
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

        private sealed class RecordingPublisher<T> : IPublisher<T>
        {
            public T Last { get; private set; }

            public void Publish(T message)
            {
                Last = message;
            }
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
    }
}
