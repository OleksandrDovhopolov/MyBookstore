using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Commands {
    public abstract class AbstractCommand : ICommand {
        // Кэшируем выполняющиеся команды, чтобы их не собрал GC во время асинхронного выполнения.
        private static readonly HashSet<ICommand> _executingCache = new HashSet<ICommand>();
        public static int CacheSize => _executingCache.Count;
        internal static void ClearExecutingCache() {
            _executingCache.Clear();
        }

        protected ICommandLogger Logger { get; }
        protected ICommandErrorReporter ErrorReporter { get; }

        public CommandError Error { get; internal protected set; } = BaseCommandsErrors.NoError;
        public string ErrorMessage => Error.Message;

        private UniTaskCompletionSource<ICommand> _promise;

        private OneShotTimer _timeoutTimer;
        private event Action<ICommand> _completeEvent;
        private event Action<ICommand> _succeedEvent;

        public CommandState State { get; private set; } = CommandState.NotStarted;
        public bool IsExecuting => State == CommandState.Executing;
        public bool IsSucceed => State == CommandState.Completed && Error == BaseCommandsErrors.NoError;
        public bool HasResult => State != CommandState.NotStarted && State != CommandState.Executing;
        public bool HasError => Error != BaseCommandsErrors.NoError;

        protected float StartTime { get; private set; }
        protected static float CurrentTime => Time.realtimeSinceStartup;

        public float ExecuteTime { get; private set; }
        public int ExecuteTimeInMs => Mathf.RoundToInt(ExecuteTime * 1000);

        protected AbstractCommand(ICommandLogger logger, ICommandErrorReporter errorReporter) {
            Logger = logger;
            ErrorReporter = errorReporter;
        }

        public ICommand SetTimeout(int milliseconds) {
            _timeoutTimer = new OneShotTimer(milliseconds);
            _timeoutTimer.OnTimePassed += OnTimeout;
            return this;
        }

        public void Execute() {
            if (State != CommandState.NotStarted) {
                return;
            }

            CommonExecutePart();
        }

        public UniTask<ICommand> ExecuteAsync() {
            if (State != CommandState.NotStarted) {
                if (_promise == null) {
                    throw new Exception($"Command was already executed with no async, cmd ={GetLogName()}, state = {State}");
                }
            }

            if (_promise != null) {
                return _promise.Task;
            }

            _promise = new UniTaskCompletionSource<ICommand>();
            CommonExecutePart();
            return _promise.Task;
        }

        private void CommonExecutePart() {
            _timeoutTimer?.Start();

            StartTime = CurrentTime;
            LogInfo($"Start at {StartTime:f2}");

            SetState(CommandState.Executing);

            try {
                ExecInternal();
            } catch (Exception e) {
                HandleExecuteException(e);
            }
        }

        protected void HandleExecuteException(Exception e) {
            ReportException(e, $"Command execute exception:{e.Message}");
            Error = new CommandError(BaseCommandsErrors.InternalCmdExceptionError, e);
            SetStateOnlyIfExecuting(CommandState.Failed);
        }

        public virtual void Terminate() {
            SetStateOnlyIfExecuting(CommandState.Terminated);
        }

        protected void TryExecInternalAsync() {
            ExecInternalAsync().Forget(HandleExecuteException);
        }

        protected abstract void ExecInternal();
        protected virtual UniTask ExecInternalAsync() { return UniTask.CompletedTask; }
        protected virtual void PostExecuteActions() {}

        public ICommand AddCompleteHandler(Action<ICommand> completeHandler) {
            _completeEvent += completeHandler;
            return this;
        }

        public void RemoveCompleteHandler(Action<ICommand> completeHandler) {
            _completeEvent -= completeHandler;
        }

        public ICommand AddSucceedHandler(Action<ICommand> succeedHandler) {
            _succeedEvent += succeedHandler;
            return this;
        }

        public void RemoveSucceedHandler(Action<ICommand> succeedHandler) {
            _succeedEvent -= succeedHandler;
        }

        protected bool HasCompleteHandler() {
            return _completeEvent != null;
        }
        protected bool HasSucceedHandler() {
            return _succeedEvent != null;
        }

        protected void NotifyComplete(CommandError error) {
            Error = error;
            NotifyComplete();
        }

        protected void NotifyComplete() {
            SetStateOnlyIfExecuting(CommandState.Completed);
        }

        public void Retry() {
            if (_promise != null) {
                LogWarning("Retry is not supported for async commands");
                return;
            }

            if (State == CommandState.NotStarted) {
                LogWarning("Try to Retry while command was not even started");
                return;
            }

            Reset();

            Execute();
        }

        public virtual void Reset() {
            StopTimeoutTimer();
            SetState(CommandState.NotStarted);
            Error = BaseCommandsErrors.NoError;
        }

        protected void ForceSetState(CommandState state) {
            SetState(state);
            if (state == CommandState.Executing) {
                StartTime = CurrentTime;
            }
        }

        private void SetStateOnlyIfExecuting(CommandState state) {
            if (State != CommandState.Executing) {
                LogWarning($"Try to set state to '{state}' when command is not in '{CommandState.Executing}' state");
                return;
            }

            SetState(state);
        }

        private void SetState(CommandState state) {
            if (State == state) {
                return;
            }
            State = state;
            switch (State) {
                case CommandState.Executing:
                    _executingCache.Add(this);
                    break;
                case CommandState.NotStarted:
                    break; // возможно при retry
                case CommandState.Completed:
                case CommandState.Failed:
                case CommandState.Terminated:
                case CommandState.Timeout:
                    try {
                        _executingCache.Remove(this);
                        OnGotExecutionResult();
                    } catch (Exception e) {
                        ReportException(e, "Exception on processing command result");
                        Error = new CommandError(BaseCommandsErrors.InternalCmdExceptionError, e);
                    } finally {
                        NotifyAboutResult();
                        _completeEvent = null;
                        _succeedEvent = null;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown state {State}");
            }
        }

        private void OnGotExecutionResult() {
            StopTimeoutTimer();
            PostExecuteActions();
            EvaluateDuration();
            ProcessEvaluatedDuration();
        }

        private void NotifyAboutResult() {
            try {
                _completeEvent?.Invoke(this);
            } catch (Exception e) {
                ReportException(e, $"Exception in {GetType().Name} completeEvent: {e.Message}");
                Error = BaseCommandsErrors.CompleteEventCmdExceptionError;
            }

            try {
                if (IsSucceed) {
                    _succeedEvent?.Invoke(this);
                }
            } catch (Exception e) {
                ReportException(e, $"Exception in {GetType().Name} succeedEvent: {e.Message}");
                Error = BaseCommandsErrors.CompleteEventCmdExceptionError;
            }

            _promise?.TrySetResult(this);
        }

        private void StopTimeoutTimer() {
            _timeoutTimer?.StopAndDispose();
            _timeoutTimer = null;
        }

        private void OnTimeout() {
            SetStateOnlyIfExecuting(CommandState.Timeout);
        }

        private void EvaluateDuration() {
            ExecuteTime = CurrentTime - StartTime;
        }

        protected virtual void ProcessEvaluatedDuration() {
            var stringBuilder = new StringBuilder(GetLogName());
            stringBuilder.Append(": Finished. ");
            if (!IsSucceed) {
                stringBuilder.Append(nameof(State)).Append(" = ").Append(State).Append(", ");

                if (Error != BaseCommandsErrors.NoError) {
                    stringBuilder.Append(nameof(Error)).Append(" = ").Append(Error).Append(", ");
                }
                if (!string.IsNullOrEmpty(ErrorMessage)) {
                    stringBuilder.Append(nameof(ErrorMessage)).Append(" = ").Append(ErrorMessage).Append(", ");
                }
            }
            stringBuilder.Append("executeTime = ").Append(ExecuteTimeInMs).Append(" ms");

            LogInfo(stringBuilder.ToString());
        }

        public override string ToString() {
            var sb = new StringBuilder(GetType().Name);
            sb.Append("(State=").Append(State);
            if (Error != BaseCommandsErrors.NoError) {
                sb.Append(", Error=").Append(Error);
            }
            sb.Append(')');
            return sb.ToString();
        }

        protected virtual void ReportException(Exception exception, string message = null) {
            try {
                ErrorReporter?.Report(exception, $"{GetLogName()}: {message}");
            } catch (Exception ex) {
                LogException(ex, $"Exception on reporting error: {ex.Message}");
            }
            LogException(exception, message);
        }

        protected void LogException(Exception exception, string logMessage = null) {
            Logger?.LogException(exception, $"{GetLogName()}: {logMessage}");
        }

        protected void LogError(string logMessage) {
            Logger?.Log(CommandLogLevel.Error, $"{GetLogName()}: {logMessage}");
        }
        protected void LogWarning(string logMessage) {
            Logger?.Log(CommandLogLevel.Warning, $"{GetLogName()}: {logMessage}");
        }
        protected void LogInfo(string logMessage) {
            Logger?.Log(CommandLogLevel.Info, $"{GetLogName()}: {logMessage}");
        }
        protected void LogDebug(string logMessage) {
            Logger?.Log(CommandLogLevel.Debug, $"{GetLogName()}: {logMessage}");
        }
        protected void LogTrace(string logMessage) {
            Logger?.Log(CommandLogLevel.Trace, $"{GetLogName()}: {logMessage}");
        }

        protected virtual string GetLogName() => GetType().Name;

        public static bool IsCommandInProgress<T>() where T : ICommand => _executingCache.Any(cmd => cmd is T);
    }
}
