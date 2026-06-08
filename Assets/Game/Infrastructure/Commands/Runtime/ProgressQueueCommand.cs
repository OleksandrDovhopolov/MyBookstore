using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Commands {
    /// <summary>
    /// Последовательная очередь прогресс-команд: распределяет проценты прогресса между шагами,
    /// шлёт общее событие progress/complete и поддерживает «фейковый» прогресс для шагов без реального прогресса.
    /// </summary>
    internal class ProgressQueueCommand : QueueCommand, IProgressQueueCommand {
        protected Dictionary<IProgressCommand, IProgressSettings> SettingsMap = new Dictionary<IProgressCommand, IProgressSettings>();

        protected int _runningProgressTotalPercent;
        protected int _runningProgressCurrentPercent;

        protected int _runningProgressFakeStep = FakeProgressSettings.FAKE_STEP_DEFAULT;
        protected int _runningProgressFakeTime = FakeProgressSettings.FAKE_TIME_MS;

        protected int _completedProgressPercent;

        public override int CurrentPercent => _completedProgressPercent + _runningProgressCurrentPercent;

        private CancellationTokenSource _fakeCts;

        internal ProgressQueueCommand(CommandFailBehaviour behaviour, string name, ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(behaviour, name, logger, errorReporter) {}

        public void AddProgress(IProgressCommand cmd, IProgressSettings settings = null) {
            settings ??= new ProgressSettings();

            if (!SettingsMap.ContainsKey(cmd)) {
                SettingsMap.Add(cmd, settings);
                base.Add(cmd);
            }
        }

        public override void Add(ICommand cmd) {
            if (cmd is IProgressCommand progressCommand) {
                AddProgress(progressCommand);
            } else {
                throw new Exception("In ProgressQueue must add only IProgressCommand!");
            }
        }

        public override void Add(IEnumerable<ICommand> list) {
            foreach (var cmd in list) {
                Add(cmd);
            }
        }

        public override void Add(params ICommand[] list) {
            foreach (var cmd in list) {
                Add(cmd);
            }
        }

        protected override void ExecInternal() {
            if (queue.Count == 0) {
                _completedProgressPercent = MAX_PERCENT;
                NotifyComplete();
                return;
            }

            _completedProgressPercent = 0;
            CheckPercents();
            NotifyProgress();

            base.ExecInternal();
        }

        protected void CheckPercents() {
            var busyPercents = CountTotalBusyPercents();
            var freeList = GetFreeSettingsList();

            var free = MAX_PERCENT - busyPercents;
            if (free > 0 && freeList.Count != 0) {
                DistributeFreePercents(free, freeList);
            }
        }

        protected int CountTotalBusyPercents() {
            return SettingsMap.Values.Sum(x => (x.Percents != ProgressSettings.CALC_AUTO) ? x.Percents : 0);
        }

        protected List<IProgressSettings> GetFreeSettingsList() {
            return SettingsMap.Values.Where(x => x.Percents == ProgressSettings.CALC_AUTO).ToList();
        }

        protected void DistributeFreePercents(int free, List<IProgressSettings> freeList) {
            int oncePercent = free / freeList.Count;
            foreach (var settings in freeList) {
                settings.Percents = oncePercent;
            }

            var restPercent = free - (oncePercent * freeList.Count);
            if (restPercent > 0) {
                freeList[^1].Percents += restPercent;
            }
        }

        protected override void NotifyProgress() {
            OnProgress(this, CurrentPercent);
        }

        public override void RetryCompletedCommand() {
            if (CompletedCommand == null) {
                return;
            }

            if (!SettingsMap.TryGetValue((IProgressCommand)CompletedCommand, out var settings)) {
                settings = ProgressSettings.ZERO;
                LogWarning($"{nameof(SettingsMap)} not contains key {CompletedCommand}, {GetLogName()}, {State}");
            }
            var percent = Mathf.Max(0, settings.Percents);
            _completedProgressPercent -= percent;
            TrySetFakeTimer(settings);
            base.RetryCompletedCommand();
        }

        protected override void Run() {
            if (State != CommandState.Executing) {
                return;
            }

            if (RunningCommand != null) {
                return;
            }

            if (queue.Count == 0) {
                NotifyComplete();
                return;
            }

            var cmd = (IProgressCommand)queue[0];
            queue.RemoveAt(0);
            RunningCommand = cmd;

            var settings = SettingsMap[cmd];
            _runningProgressTotalPercent = Mathf.Max(0, settings.Percents);

            // команда уже завершилась — выполняем следующую
            if (cmd.HasResult) {
                _completedProgressPercent += _runningProgressTotalPercent;
                Run();
            } else {
                cmd.AddCompleteHandler(OnCommandComplete);
                if (!TrySetFakeTimer(settings)) {
                    cmd.AddProgressHandler(OnCommandProgress);
                }

                cmd.Execute();
            }
        }

        private bool TrySetFakeTimer(IProgressSettings settings) {
            if (settings is FakeProgressSettings fakeSettings) {
                _runningProgressFakeStep = fakeSettings.FakeStep;
                _runningProgressFakeTime = fakeSettings.FakeTime;
                StartFakeProgress();
                return true;
            }
            return false;
        }

        private void StartFakeProgress() {
            StopFakeProgress();
            _fakeCts = new CancellationTokenSource();
            FakeProgressLoop(_fakeCts.Token).Forget();
        }

        private async UniTaskVoid FakeProgressLoop(CancellationToken token) {
            try {
                while (!token.IsCancellationRequested) {
                    await UniTask.Delay(_runningProgressFakeTime, ignoreTimeScale: true, cancellationToken: token);

                    if (_runningProgressCurrentPercent < _runningProgressTotalPercent) {
                        _runningProgressCurrentPercent += _runningProgressFakeStep;
                        NotifyProgress();
                    } else {
                        break;
                    }
                }
            } catch (OperationCanceledException) {
                // остановлено — нормально
            }
        }

        private void OnCommandProgress(ICommand cmd, int percent) {
            percent = Math.Min(percent, MAX_PERCENT);
            _runningProgressCurrentPercent = (_runningProgressTotalPercent * percent) / MAX_PERCENT;
            NotifyProgress();
        }

        protected override void OnCommandComplete(ICommand cmd) {
            StopFakeProgress();

            _completedProgressPercent += _runningProgressTotalPercent;
            _runningProgressCurrentPercent = 0;
            NotifyProgress();

            // откладываем переход к следующей команде на следующий кадр,
            // чтобы избежать глубокой рекурсии на длинных очередях
            DeferContinue(cmd).Forget();
        }

        private async UniTaskVoid DeferContinue(ICommand cmd) {
            await UniTask.Yield();
            base.OnCommandComplete(cmd);
        }

        private void StopFakeProgress() {
            _fakeCts?.Cancel();
            _fakeCts?.Dispose();
            _fakeCts = null;
        }

        protected override void PostExecuteActions() {
            CleanUp();
            base.PostExecuteActions();
        }

        public void CleanUp() {
            StopFakeProgress();
            SettingsMap?.Clear();
            queue.Clear();
        }
    }
}
