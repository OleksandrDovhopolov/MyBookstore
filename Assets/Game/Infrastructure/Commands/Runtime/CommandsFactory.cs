namespace Game.Commands {
    /// <summary>
    /// Фабрика очередей. Регистрируется в DI целевого проекта как ICommandsFactory
    /// (или создаётся вручную). Не привязана ни к какому DI-контейнеру.
    /// </summary>
    public class CommandsFactory : ICommandsFactory {
        private readonly ICommandLogger _logger;
        private readonly ICommandErrorReporter _errorReporter;

        public CommandsFactory(ICommandLogger logger, ICommandErrorReporter errorReporter) {
            _logger = logger;
            _errorReporter = errorReporter;
        }

        public IQueueCommand GetQueueCommand(string name, CommandFailBehaviour behaviour = CommandFailBehaviour.Continue) {
            return new QueueCommand(behaviour, name, _logger, _errorReporter);
        }

        public IQueueCommand GetQueueCommand(params ICommand[] commands) {
            var queue = GetQueueCommand(null, CommandFailBehaviour.Continue);
            if (commands.Length > 0) {
                foreach (var command in commands) {
                    queue.Add(command);
                }
            }
            return queue;
        }

        public IProgressQueueCommand GetProgressQueueCommand(string name, CommandFailBehaviour behaviour = CommandFailBehaviour.Continue) {
            return new ProgressQueueCommand(behaviour, name, _logger, _errorReporter);
        }

        public IProgressQueueCommand GetProgressQueueCommand(string name, params ICommand[] commands) {
            var queue = GetProgressQueueCommand(name, CommandFailBehaviour.Continue);
            if (commands.Length > 0) {
                foreach (var command in commands) {
                    queue.Add(command);
                }
            }
            return queue;
        }
    }
}
