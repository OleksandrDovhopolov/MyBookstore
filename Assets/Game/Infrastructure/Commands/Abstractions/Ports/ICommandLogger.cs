using System;

namespace Game.Commands {
    public enum CommandLogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Порт логирования. Реализуй в целевом проекте поверх своего логгера
    /// (или используй встроенный UnityCommandLogger). Команды не зависят от конкретного логгера.
    /// </summary>
    public interface ICommandLogger {
        void Log(CommandLogLevel level, string message);
        void LogException(Exception exception, string message = null);
    }
}
