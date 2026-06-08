using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Commands {
    /// <summary>
    /// Параллельная группа команд: запускает все команды одновременно, агрегирует прогресс по весам
    /// и завершается, когда отработали все (или прерывается по первой ошибке при Terminate).
    /// </summary>
    public class BoxCommand : AbstractProgressCommand {
        private readonly List<ICommand> _queue;
        private readonly List<ICommand> _activeQueue;
        private readonly Dictionary<ICommand, int> _progressMap = new();
        private readonly Dictionary<ICommand, IProgressSettings> _settingsMap = new();

        internal int QueueCount => _queue.Count;

        public ICommand CompletedCommand { get; private set; }

        public event Action<BoxCommand> CommandCompleteEvent;

        private readonly string _name;
        private readonly CommandFailBehaviour _failBehaviour;

        public BoxCommand(CommandFailBehaviour failBehavior, string name, ICommandLogger logger, ICommandErrorReporter errorReporter, params ICommand[] commands)
            : base(logger, errorReporter) {
            _queue = new List<ICommand>();
            _activeQueue = new List<ICommand>();
            _failBehaviour = failBehavior;
            _name = name;

            if (commands.Length > 0) {
                foreach (ICommand command in commands) {
                    Add(command);
                }
            }
        }

        public void Add(ICommand c, IProgressSettings settings = null) {
            settings ??= new ProgressSettings();
            _queue.Add(c);
            _settingsMap.TryAdd(c, settings);
        }

        public void AddList(IEnumerable<ICommand> list) {
            foreach (ICommand command in list) {
                Add(command);
            }
        }

        public void AddCommandCompleteHandler(Action<BoxCommand> completeHandler) {
            CommandCompleteEvent += completeHandler;
        }

        protected override void ExecInternal() {
            InitProgressMap();

            if (QueueCount == 0) {
                NotifyComplete();
                return;
            }

            foreach (var c in _queue) {
                AddHandlers(c);
            }

            CheckPercents();
            NotifyProgress();

            foreach (var c in _queue.ToArray()) {
                _activeQueue.Add(c);
                c.Execute();
            }
        }

        private void InitProgressMap() {
            foreach (var command in _queue) {
                _progressMap[command] = 0;
            }
        }

        private void AddHandlers(ICommand c) {
            c.RemoveCompleteHandler(OnCommandComplete);
            c.AddCompleteHandler(OnCommandComplete);

            if (c is IProgressCommand progressCmd) {
                progressCmd.RemoveProgressHandler(OnCommandProgress);
                progressCmd.AddProgressHandler(OnCommandProgress);
            }
        }

        private void CheckPercents() {
            var busyPercents = CountTotalBusyPercents();
            var freeList = GetFreeSettingsList();

            var free = MAX_PERCENT - busyPercents;
            if (free > 0 && freeList.Count != 0) {
                DistributeFreePercents(free, freeList);
            }
        }

        private int CountTotalBusyPercents() {
            return _settingsMap.Values.Sum(x => (x.Percents != ProgressSettings.CALC_AUTO) ? x.Percents : 0);
        }

        private List<IProgressSettings> GetFreeSettingsList() {
            return _settingsMap.Values.Where(x => x.Percents == ProgressSettings.CALC_AUTO).ToList();
        }

        private void DistributeFreePercents(int free, List<IProgressSettings> freeList) {
            int oncePercent = free / freeList.Count;
            foreach (var settings in freeList) {
                settings.Percents = oncePercent;
            }

            var restPercent = free - (oncePercent * freeList.Count);
            if (restPercent > 0) {
                freeList[^1].Percents += restPercent;
            }
        }

        private void OnCommandComplete(ICommand c) {
            if (c.IsSucceed) {
                _progressMap[c] = MAX_PERCENT;
                NotifyProgress();
            }

            CompletedCommand = c;

            CommandCompleteEvent?.Invoke(this);

            _activeQueue.Remove(CompletedCommand);

            if (!CompletedCommand.IsSucceed && _failBehaviour == CommandFailBehaviour.Terminate) {
                if (Error == BaseCommandsErrors.NoError) {
                    Error = new CommandError(BaseCommandsErrors.CommandInQueueFailedError, $" fail cmd = {CompletedCommand}");
                }
                Terminate();
                return;
            }

            _queue.Remove(CompletedCommand);

            if (QueueCount == 0) {
                NotifyComplete();
            }
        }

        private void OnCommandProgress(IProgressCommand c, int percent) {
            _progressMap[c] = percent;
            NotifyProgress();
        }

        private void NotifyProgress() {
            var totalProgress = 0;

            foreach (var (cmd, value) in _progressMap) {
                var settingsPercent = _settingsMap[cmd].Percents;
                if (settingsPercent == 0) {
                    continue;
                }
                var koeff = (float)settingsPercent / MAX_PERCENT;
                totalProgress += Mathf.CeilToInt(value * koeff);
            }

            OnProgress(Mathf.Min(MAX_PERCENT, totalProgress));
        }

        protected override void PostExecuteActions() {
            foreach (var cmd in _activeQueue.ToArray()) {
                cmd.Terminate();
            }

            base.PostExecuteActions();
        }

        protected override string GetLogName() {
            var str = base.GetLogName();
            if (string.IsNullOrEmpty(_name)) {
                return str;
            }

            str += " " + _name + " ";
            return str;
        }

        public override void Reset() {
            base.Reset();
            foreach (var command in _queue) {
                command.Reset();
            }
        }
    }
}
