using System;
using UnityEngine;

namespace Game.Commands {
    /// <summary>
    /// Базовая реализация порта логирования поверх UnityEngine.Debug.
    /// Заменяй на свою (ZLogger/Serilog/собственный канал), если нужно.
    /// </summary>
    public class UnityCommandLogger : ICommandLogger {
        private readonly CommandLogLevel _minimumLevel;

        public UnityCommandLogger(CommandLogLevel minimumLevel = CommandLogLevel.Info) {
            _minimumLevel = minimumLevel;
        }

        public void Log(CommandLogLevel level, string message) {
            if (level < _minimumLevel) {
                return;
            }

            switch (level) {
                case CommandLogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case CommandLogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }

        public void LogException(Exception exception, string message = null) {
            if (!string.IsNullOrEmpty(message)) {
                Debug.LogError(message);
            }
            if (exception != null) {
                Debug.LogException(exception);
            }
        }
    }
}
