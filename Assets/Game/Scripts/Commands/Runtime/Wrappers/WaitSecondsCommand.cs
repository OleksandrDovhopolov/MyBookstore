using System;
using Cysharp.Threading.Tasks;

namespace Game.Commands {
    /// <summary>
    /// Команда-задержка на заданное число секунд (без учёта timeScale).
    /// </summary>
    public class WaitSecondsCommand : AbstractFakeProgressCommand {
        private readonly float _seconds;

        public WaitSecondsCommand(float seconds, ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {
            _seconds = seconds;
        }

        protected override void ExecInternal() {
            TryExecInternalAsync();
        }

        protected override async UniTask ExecInternalAsync() {
            await UniTask.Delay(TimeSpan.FromSeconds(_seconds), ignoreTimeScale: true);
            NotifyComplete();
        }

        protected override string GetLogName() {
            return base.GetLogName() + $" {nameof(_seconds)} = {_seconds}";
        }
    }
}
