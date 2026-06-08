using System;
using System.Collections.Generic;

namespace Game.Commands {
    /// <summary>
    /// Последовательная очередь команд. Шлёт общий progress/complete для всей очереди,
    /// поддерживает поведение при ошибке (Continue/Terminate) и ручной режим выполнения.
    /// </summary>
    internal class QueueCommand : AbstractProgressCommand, IQueueCommand {
        protected readonly List<ICommand> queue = new List<ICommand>();
        public int QueueCount => queue.Count;

        protected int? CountInQueueOnStart;

        public ICommand CompletedCommand { get; protected set; }

        public ICommand RunningCommand { get; protected set; }

        internal bool SomeCmdInQueueNoCompleted { get; private set; }

        /// <summary>Выполнена очередная команда; ссылка на неё — в CompletedCommand.</summary>
        public event Action<IQueueCommand> CommandCompleteEvent;

        private readonly string _name;

        internal readonly CommandFailBehaviour _failBehaviour;

        private QueueExecuteMode _mode = QueueExecuteMode.Auto;

        internal QueueCommand(CommandFailBehaviour behaviour, string name, ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {
            _failBehaviour = behaviour;
            _name = name;
        }

        public void SetExecuteMode(QueueExecuteMode mode) {
            if (State != CommandState.NotStarted) {
                return;
            }
            _mode = mode;
        }

        public bool IsContains(ICommand cmd) {
            return queue.Contains(cmd);
        }

        public virtual void Add(ICommand c) {
            queue.Add(c);
        }

        public virtual void Add(IEnumerable<ICommand> list) {
            queue.AddRange(list);
        }

        public virtual void Add(params ICommand[] list) {
            queue.AddRange(list);
        }

        public void AddCommandCompleteHandler(Action<IQueueCommand> completeHandler) {
            CommandCompleteEvent += completeHandler;
        }

        public virtual void ContinueExecute() {
            Run();
        }

        public virtual void RetryCompletedCommand() {
            if (CompletedCommand == null) {
                return;
            }
            NotifyProgress();
            RunningCommand = CompletedCommand;
            RunningCommand.AddCompleteHandler(OnCommandComplete);
            RunningCommand.Retry();
        }

        protected override void ExecInternal() {
            CountInQueueOnStart = queue.Count;
            SomeCmdInQueueNoCompleted = false;
            Run();
        }

        public override void Reset() {
            throw new NotImplementedException("This queue can't be reset");
        }

        protected override void PostExecuteActions() {
            CommandCompleteEvent = null;
            RunningCommand?.Terminate();
            RunningCommand = null;
            base.PostExecuteActions();
        }

        protected virtual void Run() {
            if (State != CommandState.Executing) {
                return;
            }

            NotifyProgress();

            if (RunningCommand != null) {
                return;
            }

            if (queue.Count == 0) {
                NotifyComplete();
                return;
            }

            RunningCommand = queue.GetAndRemove(0);
            RunningCommand.AddCompleteHandler(OnCommandComplete);
            RunningCommand.Execute();
        }

        protected virtual void OnCommandComplete(ICommand cmd) {
            if (RunningCommand == null) {
                return;
            }
            CompletedCommand = RunningCommand;
            RunningCommand = null;

            CommandCompleteEvent?.Invoke(this);

            if (_mode == QueueExecuteMode.Manual) {
                return;
            }

            if (CompletedCommand != null && !CompletedCommand.IsSucceed) {
                SomeCmdInQueueNoCompleted = true;
                if (_failBehaviour == CommandFailBehaviour.Terminate) {
                    if (Error == BaseCommandsErrors.NoError) {
                        Error = new CommandError(BaseCommandsErrors.CommandInQueueFailedError, $" fail cmd = {CompletedCommand}");
                    }
                    Terminate();
                    return;
                }
            }

            ContinueExecute();
        }

        protected virtual void NotifyProgress() {
            var countInQueueOnStart = CountInQueueOnStart ?? 0;

            OnProgress(this, countInQueueOnStart == 0
                ? MAX_PERCENT
                : MAX_PERCENT * (countInQueueOnStart - GetCurrentProgressCmdCount()) / countInQueueOnStart);
        }

        private int GetCurrentProgressCmdCount() {
            if (RunningCommand != null) {
                return QueueCount + 1;
            }
            return QueueCount;
        }

        protected override string GetLogName() {
            var str = base.GetLogName();
            if (string.IsNullOrEmpty(_name)) {
                return str;
            }

            str += " " + _name + " ";
            return str;
        }
    }
}
