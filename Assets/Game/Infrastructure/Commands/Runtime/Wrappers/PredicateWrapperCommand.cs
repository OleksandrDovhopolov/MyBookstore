using System;

namespace Game.Commands {
    /// <summary>
    /// Команда, успех которой определяется предикатом.
    /// </summary>
    public class PredicateWrapperCommand : AbstractFakeProgressCommand {
        private readonly Func<bool> _predicate;
        private readonly string _logName;

        public PredicateWrapperCommand(Func<bool> predicate, ICommandLogger logger, ICommandErrorReporter errorReporter, string logName = null)
            : base(logger, errorReporter) {
            _predicate = predicate;
            _logName = logName ?? predicate.Method.Name;
        }

        protected override void ExecInternal() {
            var isSucceed = _predicate.Invoke();
            if (!isSucceed) {
                Error = new CommandError(nameof(PredicateWrapperCommand));
            }
            NotifyComplete();
        }

        protected override string GetLogName() {
            return _logName;
        }
    }
}
