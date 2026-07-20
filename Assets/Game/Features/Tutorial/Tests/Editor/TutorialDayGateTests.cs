using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.DayCycle.Day;
using Game.Tutorial.API;
using Game.Tutorial.Services;
using NUnit.Framework;
using Save;
using UnityEngine;

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

        private static TutorialService BuildService(
            FakeDayProgress dayProgress,
            FakeGameFlow gameFlow,
            FakeSaveService save = null)
        {
            var dayOne = new FakeSequence
            {
                Id = "tutorial_day_1",
                Priority = 10,
                IsEligibleFunc = () => dayProgress.Current.CurrentDay == 1
            };
            return new TutorialService(
                save ?? new FakeSaveService(),
                new ITutorialSequence[] { dayOne },
                hubReadySub: null,
                startedPub: null,
                stepPub: null,
                completedPub: null,
                dayProgress: dayProgress,
                gameFlow: gameFlow);
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
    }
}
