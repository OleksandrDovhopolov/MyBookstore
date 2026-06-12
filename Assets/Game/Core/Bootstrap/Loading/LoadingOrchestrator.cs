using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Logging;

namespace Game.Bootstrap.Loading
{
    public sealed class LoadingOrchestrator
    {
        private readonly LoadingProgressAggregator _aggregator;
        private readonly IChannelLogger<LogChannel.Loading> _logger;
        private readonly TimeSpan _progressPollInterval = TimeSpan.FromMilliseconds(100);
        private readonly List<ILoadingOperation> _allOperations = new();

        private IReadOnlyList<LoadingPhase> _phases = Array.Empty<LoadingPhase>();
        private float _lastReportedProgress;

        public LoadingOrchestrator(LoadingProgressAggregator aggregator)
            : this(aggregator, null)
        {
        }

        public LoadingOrchestrator(LoadingProgressAggregator aggregator, ILogService logService)
        {
            _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
            _logger = logService?.GetLogger<LogChannel.Loading>();
        }

        public event Action<float> ProgressChanged;
        public event Action<string> ActiveDescriptionChanged;
        public event Action<LoadingFailure> CriticalFailure;

        public string CurrentActiveDescription { get; private set; } = string.Empty;
        public float CurrentProgress { get; private set; }

        public void SetPhases(IReadOnlyList<LoadingPhase> phases)
        {
            _phases = phases ?? throw new ArgumentNullException(nameof(phases));
            _allOperations.Clear();
            foreach (var phase in _phases)
            {
                foreach (var group in phase.Groups)
                {
                    _allOperations.AddRange(group.Operations);
                }
            }

            ResetAllOperations();
        }

        public void ResetFromPhase(int phaseIndex)
        {
            if (_phases.Count == 0)
            {
                return;
            }

            var clamped = Math.Clamp(phaseIndex, 0, _phases.Count - 1);
            for (var i = clamped; i < _phases.Count; i++)
            {
                foreach (var group in _phases[i].Groups)
                {
                    foreach (var operation in group.Operations)
                    {
                        operation.Reset();
                    }
                }
            }

            RefreshProgress();
        }

        public async UniTask<LoadingRunResult> RunAsync(int startPhaseIndex, TimeSpan? globalTimeout, CancellationToken ct)
        {
            if (_phases.Count == 0)
            {
                throw new InvalidOperationException("No loading phases configured.");
            }

            ct.ThrowIfCancellationRequested();
            var clampedStart = Math.Clamp(startPhaseIndex, 0, _phases.Count - 1);
            using var globalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (globalTimeout.HasValue && globalTimeout.Value > TimeSpan.Zero)
            {
                globalCts.CancelAfter(globalTimeout.Value);
            }

            for (var phaseIndex = clampedStart; phaseIndex < _phases.Count; phaseIndex++)
            {
                var phase = _phases[phaseIndex];
                foreach (var group in phase.Groups)
                {
                    LoadingFailure failure = group.ExecutionMode == LoadingGroupExecutionMode.Sequential
                        ? await ExecuteSequentialGroupAsync(phase, group, globalCts.Token, ct)
                        : await ExecuteParallelGroupAsync(phase, group, globalCts.Token, ct);

                    if (failure is { IsCritical: true })
                    {
                        CriticalFailure?.Invoke(failure);
                        return LoadingRunResult.Failed(failure, phaseIndex);
                    }
                }
            }

            CurrentProgress = 1f;
            ProgressChanged?.Invoke(CurrentProgress);
            return LoadingRunResult.Success();
        }

        private async UniTask<LoadingFailure> ExecuteSequentialGroupAsync(
            LoadingPhase phase,
            LoadingGroup group,
            CancellationToken groupToken,
            CancellationToken rootToken)
        {
            foreach (var operation in group.Operations)
            {
                SetActiveDescription(operation.Description);
                var failure = await ExecuteOperationWithPolicyAsync(phase, group, operation, groupToken, rootToken);
                RefreshProgress();

                if (failure == null)
                {
                    continue;
                }

                if (failure.IsCritical)
                {
                    return failure;
                }
            }

            return null;
        }

        private async UniTask<LoadingFailure> ExecuteParallelGroupAsync(
            LoadingPhase phase,
            LoadingGroup group,
            CancellationToken groupToken,
            CancellationToken rootToken)
        {
            var groupCts = CancellationTokenSource.CreateLinkedTokenSource(groupToken);
            var progressLoopCts = CancellationTokenSource.CreateLinkedTokenSource(groupCts.Token);

            try
            {
                var groupExecutionToken = groupCts.Token;
                Action cancelGroup = groupCts.Cancel;

                var tasks = group.Operations
                    .Select(op => RunOperationAsync(op, groupExecutionToken, rootToken, cancelGroup))
                    .ToArray();

                var progressLoop = PollParallelProgressAsync(group.Operations, progressLoopCts.Token);
                var failures = await UniTask.WhenAll(tasks);

                progressLoopCts.Cancel();
                await progressLoop.SuppressCancellationThrow();

                return failures.FirstOrDefault(f => f is { IsCritical: true });

                async UniTask<LoadingFailure> RunOperationAsync(
                    ILoadingOperation operation,
                    CancellationToken executionToken,
                    CancellationToken root,
                    Action cancel)
                {
                    try
                    {
                        var failure = await ExecuteOperationWithPolicyAsync(
                            phase, group, operation, executionToken, root);

                        if (failure is { IsCritical: true })
                        {
                            cancel();
                        }

                        return failure;
                    }
                    catch (OperationCanceledException) when (executionToken.IsCancellationRequested && !root.IsCancellationRequested)
                    {
                        return null;
                    }
                }
            }
            finally
            {
                progressLoopCts.Dispose();
                groupCts.Dispose();
            }
        }

        private async UniTask PollParallelProgressAsync(IReadOnlyList<ILoadingOperation> activeOperations, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var active = activeOperations
                    .Where(x => x.Status == LoadingOperationStatus.InProgress)
                    .OrderByDescending(x => x.DisplayPriority)
                    .FirstOrDefault();

                if (active != null)
                {
                    SetActiveDescription(active.Description);
                }

                RefreshProgress();
                await UniTask.Delay(_progressPollInterval, cancellationToken: ct);
            }
        }

        private async UniTask<LoadingFailure> ExecuteOperationWithPolicyAsync(
            LoadingPhase phase,
            LoadingGroup group,
            ILoadingOperation operation,
            CancellationToken operationGroupToken,
            CancellationToken rootToken)
        {
            var maxAttempts = operation.RetryPolicy.MaxAttempts;
            var delay = operation.RetryPolicy.DelayBetweenAttempts;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                rootToken.ThrowIfCancellationRequested();
                operation.Reset();
                var sw = Stopwatch.StartNew();
                var timedOut = false;
                try
                {
                    using var opCts = CancellationTokenSource.CreateLinkedTokenSource(operationGroupToken);
                    if (operation.Timeout.HasValue && operation.Timeout.Value > TimeSpan.Zero)
                    {
                        opCts.CancelAfter(operation.Timeout.Value);
                    }

                    LogOperation("start", phase.Id, group.Id, operation, attempt, false, null, sw.ElapsedMilliseconds);
                    await operation.ExecuteAsync(opCts.Token);
                    sw.Stop();
                    LogOperation("completed", phase.Id, group.Id, operation, attempt, false, null, sw.ElapsedMilliseconds);
                    return null;
                }
                catch (OperationCanceledException ex) when (!operationGroupToken.IsCancellationRequested)
                {
                    sw.Stop();
                    timedOut = true;
                    LogOperation("timeout", phase.Id, group.Id, operation, attempt, true, ex, sw.ElapsedMilliseconds);
                    if (attempt < maxAttempts)
                    {
                        await UniTask.Delay(delay, cancellationToken: rootToken);
                        continue;
                    }

                    return BuildFailure(phase, group, operation, attempt, timedOut, ex);
                }
                catch (OperationCanceledException ex) when (operationGroupToken.IsCancellationRequested && !rootToken.IsCancellationRequested)
                {
                    sw.Stop();
                    timedOut = true;
                    LogOperation("timeout", phase.Id, group.Id, operation, attempt, true, ex, sw.ElapsedMilliseconds);
                    return BuildFailure(phase, group, operation, attempt, timedOut, ex);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    LogOperation("failed", phase.Id, group.Id, operation, attempt, false, ex, sw.ElapsedMilliseconds);
                    if (attempt < maxAttempts)
                    {
                        await UniTask.Delay(delay, cancellationToken: rootToken);
                        continue;
                    }

                    return BuildFailure(phase, group, operation, attempt, timedOut, ex);
                }
            }

            return null;
        }

        private LoadingFailure BuildFailure(
            LoadingPhase phase,
            LoadingGroup group,
            ILoadingOperation operation,
            int attempt,
            bool timedOut,
            Exception ex)
        {
            return new LoadingFailure(
                phase.Id,
                group.Id,
                operation.Id,
                attempt,
                timedOut,
                operation.IsCritical,
                ex);
        }

        private void LogOperation(
            string result,
            string phaseId,
            string groupId,
            ILoadingOperation operation,
            int attempt,
            bool timedOut,
            Exception ex,
            long durationMs)
        {
            var level = ResolveLogLevel(result, operation.IsCritical, ex);
            var message = BuildOperationLogMessage(
                result,
                phaseId,
                groupId,
                operation,
                attempt,
                timedOut,
                ex,
                durationMs);
            var payload = new
            {
                result,
                phaseId,
                groupId,
                operationId = operation.Id,
                attempt,
                durationMs,
                isCritical = operation.IsCritical,
                timedOut,
                errorType = ex?.GetType().Name
            };

            _logger?.Log(level, message, ex, payload);
        }

        private static LogLevel ResolveLogLevel(string result, bool isCritical, Exception ex)
        {
            if (string.Equals(result, "start", StringComparison.Ordinal))
            {
                return LogLevel.Debug;
            }

            if (ex == null)
            {
                return LogLevel.Information;
            }

            if (timedOutOrFailed(result) && isCritical)
            {
                return LogLevel.Error;
            }

            return LogLevel.Warning;

            static bool timedOutOrFailed(string value)
            {
                return string.Equals(value, "timeout", StringComparison.Ordinal)
                       || string.Equals(value, "failed", StringComparison.Ordinal);
            }
        }

        private static string BuildOperationLogMessage(
            string result,
            string phaseId,
            string groupId,
            ILoadingOperation operation,
            int attempt,
            bool timedOut,
            Exception ex,
            long durationMs)
        {
            var sb = new StringBuilder(256);
            sb.Append("[Loading] ");
            sb.Append("result=").Append(result).Append(' ');
            sb.Append("phase_id=").Append(phaseId).Append(' ');
            sb.Append("group_id=").Append(groupId).Append(' ');
            sb.Append("operation_id=").Append(operation.Id).Append(' ');
            sb.Append("attempt=").Append(attempt).Append(' ');
            sb.Append("duration_ms=").Append(durationMs).Append(' ');
            sb.Append("is_critical=").Append(operation.IsCritical).Append(' ');
            sb.Append("timed_out=").Append(timedOut);
            if (ex != null)
            {
                sb.Append(' ');
                sb.Append("error_type=").Append(ex.GetType().Name).Append(' ');
                sb.Append("error_message=").Append(ex.Message);
            }

            return sb.ToString();
        }

        private void ResetAllOperations()
        {
            foreach (var operation in _allOperations)
            {
                operation.Reset();
            }

            _aggregator.Reset();
            _lastReportedProgress = 0f;
            CurrentProgress = 0f;
            SetActiveDescription(string.Empty);
            ProgressChanged?.Invoke(CurrentProgress);
        }

        private void SetActiveDescription(string description)
        {
            var next = description ?? string.Empty;
            if (string.Equals(CurrentActiveDescription, next, StringComparison.Ordinal))
            {
                return;
            }

            CurrentActiveDescription = next;
            ActiveDescriptionChanged?.Invoke(CurrentActiveDescription);
        }

        private void RefreshProgress()
        {
            var smoothed = _aggregator.Update(_allOperations);
            if (Math.Abs(smoothed - _lastReportedProgress) < 0.0001f)
            {
                return;
            }

            _lastReportedProgress = smoothed;
            CurrentProgress = smoothed;
            ProgressChanged?.Invoke(smoothed);
        }
    }
}
