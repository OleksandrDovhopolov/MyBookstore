using System;

namespace Game.Commands {
    /// <summary>
    /// Команда с прогрессом: события "complete" и "progress".
    /// </summary>
    public abstract class AbstractProgressCommand : AbstractCommand, IProgressCommand {
        public const int MAX_PERCENT = 100;

        private event Action<IProgressCommand, int> _progressEvent;

        public virtual int CurrentPercent { get; private set; }

        protected AbstractProgressCommand(ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {}

        public IProgressCommand AddProgressHandler(Action<IProgressCommand, int> progressHandler) {
            if (progressHandler != null) {
                _progressEvent += progressHandler;
            }
            return this;
        }

        public IProgressCommand RemoveProgressHandler(Action<IProgressCommand, int> progressHandler) {
            if (progressHandler != null) {
                _progressEvent -= progressHandler;
            }
            return this;
        }

        protected bool HasProgressHandler() {
            return _progressEvent != null;
        }

        protected virtual void OnProgress(int percent) {
            CurrentPercent = percent;
            _progressEvent?.Invoke(this, percent);
        }

        protected void OnProgress(ICommand cmd, int percent) {
            OnProgress(percent);
        }

        protected override void PostExecuteActions() {
            if (IsSucceed) {
                OnProgress(MAX_PERCENT);
            }
            _progressEvent = null;
            base.PostExecuteActions();
        }
    }
}
