using System;

namespace Game.Commands {
    /// <summary>
    /// Команда-«заглушка» прогресса: реализует IProgressCommand, но не шлёт реальных событий прогресса.
    /// Удобна как база для мгновенных/обёрточных команд внутри прогресс-очередей.
    /// </summary>
    public abstract class AbstractFakeProgressCommand : AbstractCommand, IProgressCommand {
        protected AbstractFakeProgressCommand(ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {
            CurrentPercent = 0;
        }

        public int CurrentPercent { get; }

        public IProgressCommand AddProgressHandler(Action<IProgressCommand, int> progressHandler) {
            return this;
        }

        public IProgressCommand RemoveProgressHandler(Action<IProgressCommand, int> progressHandler) {
            return this;
        }
    }
}
