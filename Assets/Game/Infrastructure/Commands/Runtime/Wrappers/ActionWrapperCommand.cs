using System;
using Cysharp.Threading.Tasks;

namespace Game.Commands {
    /// <summary>
    /// Оборачивает обычный Action или Func&lt;UniTask&gt; в команду.
    /// </summary>
    public class ActionWrapperCommand : AbstractFakeProgressCommand {
        private readonly Func<UniTask> _func;
        private readonly Action _action;
        private readonly string _methodName;

        private readonly bool _useAction;

        public ActionWrapperCommand(Action action, ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {
            _action = action;
            _methodName = _action.Method.Name;
            _useAction = true;
        }

        public ActionWrapperCommand(Func<UniTask> func, ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {
            _func = func;
            _methodName = TryGetGenericName(_func.Method.Name);
        }

        private string TryGetGenericName(string methodName) {
            int beginIdx = methodName.IndexOf('<');
            int endIdx = methodName.LastIndexOf('>');

            if (beginIdx == -1 || endIdx == -1) {
                return methodName;
            }
            return methodName.Substring(beginIdx + 1, endIdx - beginIdx - 1);
        }

        protected override void ExecInternal() {
            if (_useAction) {
                _action.Invoke();
                NotifyComplete();
            } else {
                TryExecInternalAsync();
            }
        }

        protected override async UniTask ExecInternalAsync() {
            await _func.Invoke();
            NotifyComplete();
        }

        protected override string GetLogName() {
            return _methodName;
        }
    }
}
