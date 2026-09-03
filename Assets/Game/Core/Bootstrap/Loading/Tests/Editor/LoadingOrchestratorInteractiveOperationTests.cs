using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Bootstrap.Loading.Tests.Editor
{
    /// <summary>
    /// REL-5: an operation that waits on a human (the first-run privacy gate) must not be killed by the
    /// global loading deadline, and must not poison the deadline for everything downstream.
    /// </summary>
    public sealed class LoadingOrchestratorInteractiveOperationTests
    {
        private static readonly TimeSpan ShortGlobalTimeout = TimeSpan.FromMilliseconds(50);

        [UnityTest]
        public IEnumerator RunAsync_InteractiveOperationOutlivesGlobalTimeout_Succeeds()
        {
            var completed = false;
            var gate = new InteractiveTestOperation(
                id: "consent_gate",
                isCritical: true,
                executeAsync: async ct =>
                {
                    // Far longer than the global budget: this is the player reading the notice.
                    await UniTask.Delay(TimeSpan.FromMilliseconds(300), cancellationToken: ct);
                    completed = true;
                });

            var orchestrator = BuildOrchestrator(gate);

            LoadingRunResult result = default;
            yield return ToCoroutine(
                orchestrator.RunAsync(0, ShortGlobalTimeout, CancellationToken.None),
                value => result = value);

            Assert.That(completed, Is.True, "the interactive operation was cancelled mid-wait");
            Assert.That(result.IsSuccess, Is.True, "the global deadline was not suspended");
        }

        [UnityTest]
        public IEnumerator RunAsync_AfterInteractiveOperation_SubsequentOperationsStillExecute()
        {
            var followUpRan = false;

            var gate = new InteractiveTestOperation(
                id: "consent_gate",
                isCritical: true,
                executeAsync: ct => UniTask.Delay(TimeSpan.FromMilliseconds(300), cancellationToken: ct));

            var followUp = new TestOperation(
                id: "remote_config_init",
                isCritical: true,
                executeAsync: async ct =>
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    followUpRan = true;
                });

            var orchestrator = BuildOrchestrator(gate, followUp);

            LoadingRunResult result = default;
            yield return ToCoroutine(
                orchestrator.RunAsync(0, ShortGlobalTimeout, CancellationToken.None),
                value => result = value);

            Assert.That(followUpRan, Is.True, "the resumed deadline cancelled the next operation");
            Assert.That(result.IsSuccess, Is.True);
        }

        [UnityTest]
        public IEnumerator RunAsync_NonInteractiveOperationOutlivesGlobalTimeout_StillFails()
        {
            // The safety net must stay intact for ordinary (e.g. hung network) operations.
            var hung = new TestOperation(
                id: "hung_op",
                isCritical: true,
                executeAsync: ct => UniTask.Delay(TimeSpan.FromMilliseconds(300), cancellationToken: ct));

            var orchestrator = BuildOrchestrator(hung);

            LoadingRunResult result = default;
            yield return ToCoroutine(
                orchestrator.RunAsync(0, ShortGlobalTimeout, CancellationToken.None),
                value => result = value);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure.TimedOut, Is.True);
        }

        private static LoadingOrchestrator BuildOrchestrator(params ILoadingOperation[] operations)
        {
            var phase = new LoadingPhase("phase_interactive", new[]
            {
                new LoadingGroup("group_seq", LoadingGroupExecutionMode.Sequential, operations)
            });

            var orchestrator = new LoadingOrchestrator(new LoadingProgressAggregator(1f));
            orchestrator.SetPhases(new[] { phase });
            return orchestrator;
        }

        /// <summary>Minimal ILoadingOperation driven by a delegate. No retries, no per-op timeout.</summary>
        private class TestOperation : ILoadingOperation
        {
            private readonly Func<CancellationToken, UniTask> _executeAsync;

            public TestOperation(string id, bool isCritical, Func<CancellationToken, UniTask> executeAsync)
            {
                Id = id;
                Description = id;
                IsCritical = isCritical;
                _executeAsync = executeAsync;
            }

            public string Id { get; }
            public string Description { get; }
            public LoadingOperationStatus Status { get; private set; } = LoadingOperationStatus.NotStarted;
            public float Progress { get; private set; }
            public float Weight => 1f;
            public bool IsCritical { get; }
            public int DisplayPriority => 0;
            public LoadingRetryPolicy RetryPolicy => LoadingRetryPolicy.None;
            public TimeSpan? Timeout => null;

            public async UniTask ExecuteAsync(CancellationToken ct)
            {
                Status = LoadingOperationStatus.InProgress;
                await _executeAsync(ct);
                Progress = 1f;
                Status = LoadingOperationStatus.Completed;
            }

            public void Reset()
            {
                Status = LoadingOperationStatus.NotStarted;
                Progress = 0f;
            }
        }

        /// <summary>The same operation, marked as blocking on a human.</summary>
        private sealed class InteractiveTestOperation : TestOperation, IInteractiveLoadingOperation
        {
            public InteractiveTestOperation(string id, bool isCritical, Func<CancellationToken, UniTask> executeAsync)
                : base(id, isCritical, executeAsync)
            {
            }
        }

        private static IEnumerator ToCoroutine<T>(UniTask<T> task, Action<T> onResult)
        {
            var wrappedTask = task.AsTask();
            while (!wrappedTask.IsCompleted)
            {
                yield return null;
            }

            if (wrappedTask.IsFaulted)
            {
                throw wrappedTask.Exception;
            }

            onResult(wrappedTask.Result);
        }
    }
}
