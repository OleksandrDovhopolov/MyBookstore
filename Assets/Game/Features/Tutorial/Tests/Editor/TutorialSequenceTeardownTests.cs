using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Tutorial.API;
using Game.Tutorial.Services;
using NUnit.Framework;
using Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialSequenceTeardownTests
    {
        [Test]
        public async Task RunCompleted_CallsOnRunEnded()
        {
            var sequence = new FakeSequence
            {
                Steps = new ITutorialStep[] { new InstantStep("done") }
            };
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(sequence, gameFlow);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(service.IsRunning);
                Assert.AreEqual(1, sequence.OnRunEndedCallCount);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task RunStarted_ThenRunEnded_HooksCalledInOrder()
        {
            var order = new List<string>();
            var sequence = new FakeSequence
            {
                Order = order,
                Steps = new ITutorialStep[] { new RecordingStep("record", order) }
            };
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(sequence, gameFlow);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);
                await UniTask.Yield(PlayerLoopTiming.Update);

                CollectionAssert.AreEqual(new[] { "started", "step", "ended" }, order);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task OnRunStartedThrew_ServiceStillIdle()
        {
            var sequence = new FakeSequence { ThrowOnRunStarted = true };
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(sequence, gameFlow);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[Tutorial\] sequence 'tutorial_test' failed: .*startup failed"));
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(service.IsRunning);
                Assert.AreEqual(1, sequence.OnRunStartedCallCount);
                Assert.AreEqual(1, sequence.OnRunEndedCallCount);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task RunCancelled_CallsOnRunEnded()
        {
            var sequence = new FakeSequence
            {
                Steps = new ITutorialStep[] { new BlockingStep("hold") }
            };
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(sequence, gameFlow);
            try
            {
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);
                await UniTask.Yield(PlayerLoopTiming.Update);

                await service.SkipActiveAsync(CancellationToken.None);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(service.IsRunning);
                Assert.AreEqual(1, sequence.OnRunEndedCallCount);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task StepThrew_CallsOnRunEnded()
        {
            var sequence = new FakeSequence
            {
                Steps = new ITutorialStep[] { new ThrowingStep("throw") }
            };
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(sequence, gameFlow);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[Tutorial\] sequence 'tutorial_test' failed: .*step failed"));
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(service.IsRunning);
                Assert.AreEqual(1, sequence.OnRunEndedCallCount);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task OnRunEndedThrew_ServiceStillIdle()
        {
            var sequence = new FakeSequence
            {
                ThrowOnRunEnded = true,
                Steps = new ITutorialStep[] { new ThrowingStep("throw") }
            };
            var gameFlow = new FakeGameFlow { IsLocationLoaded = true };
            var service = BuildService(sequence, gameFlow);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[Tutorial\] sequence 'tutorial_test' failed: .*step failed"));
                LogAssert.Expect(LogType.Error, new Regex(@"\[Tutorial\] sequence 'tutorial_test' teardown failed: .*teardown failed"));
                await service.AfterLoadAsync(CancellationToken.None);
                gameFlow.RaiseLocationLoaded(true);
                await UniTask.Yield(PlayerLoopTiming.Update);

                Assert.IsFalse(service.IsRunning);
                Assert.AreEqual(1, sequence.OnRunEndedCallCount);
                sequence.ThrowOnRunEnded = false;
                sequence.Steps = new ITutorialStep[] { new BlockingStep("hold_after_teardown_error") };

                Assert.IsTrue(await service.TryStartAsync(sequence.Id, true, CancellationToken.None));
                Assert.IsTrue(service.IsRunning);
            }
            finally
            {
                service.Dispose();
            }
        }

        private static TutorialService BuildService(FakeSequence sequence, FakeGameFlow gameFlow)
            => new(
                new FakeSaveService(),
                new ITutorialSequence[] { sequence },
                hubReadySub: null,
                startedPub: null,
                stepPub: null,
                completedPub: null,
                gameFlow: gameFlow);

        private sealed class FakeSequence : ITutorialSequence
        {
            public string Id { get; set; } = "tutorial_test";
            public int Priority { get; set; } = 10;
            public TutorialContext Context { get; set; } = TutorialContext.Location;
            public TutorialTrigger Trigger { get; set; } = TutorialTrigger.LocationLoaded;
            public string TriggerParam { get; set; }
            public TutorialResumePolicy ResumePolicy { get; set; } = TutorialResumePolicy.Restart;
            public IReadOnlyList<ITutorialStep> Steps { get; set; } = new ITutorialStep[] { new InstantStep("done") };
            public List<string> Order { get; set; }
            public int OnRunEndedCallCount { get; private set; }
            public int OnRunStartedCallCount { get; private set; }
            public bool ThrowOnRunStarted { get; set; }
            public bool ThrowOnRunEnded { get; set; }

            public bool IsEligible() => true;
            public IReadOnlyList<ITutorialStep> GetSteps() => Steps;

            public void OnRunStarted()
            {
                OnRunStartedCallCount++;
                Order?.Add("started");
                if (ThrowOnRunStarted)
                    throw new InvalidOperationException("startup failed");
            }

            public void OnRunEnded()
            {
                OnRunEndedCallCount++;
                Order?.Add("ended");
                if (ThrowOnRunEnded)
                    throw new InvalidOperationException("teardown failed");
            }
        }

        private sealed class RecordingStep : ITutorialStep
        {
            private readonly List<string> _order;

            public RecordingStep(string id, List<string> order)
            {
                Id = id;
                _order = order;
            }

            public string Id { get; }

            public UniTask ExecuteAsync(CancellationToken ct)
            {
                _order.Add("step");
                return UniTask.CompletedTask;
            }
        }

        private sealed class InstantStep : ITutorialStep
        {
            public InstantStep(string id) => Id = id;

            public string Id { get; }

            public UniTask ExecuteAsync(CancellationToken ct) => UniTask.CompletedTask;
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

        private sealed class ThrowingStep : ITutorialStep
        {
            public ThrowingStep(string id) => Id = id;

            public string Id { get; }

            public UniTask ExecuteAsync(CancellationToken ct)
                => throw new InvalidOperationException("step failed");
        }

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _modules = new();

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
