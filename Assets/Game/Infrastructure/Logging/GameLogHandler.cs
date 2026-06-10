using System;
using System.Globalization;
using UnityEngine;

namespace Game.Logging
{
    public sealed class GameLogHandler : ILogHandler
    {
        private readonly ILogHandler _previousHandler;
        private readonly Action<LogEntry> _sink;

        public GameLogHandler(ILogHandler previousHandler, Action<LogEntry> sink)
        {
            _previousHandler = previousHandler ?? Debug.unityLogger.logHandler;
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public ILogHandler PreviousHandler => _previousHandler;

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            var entry = new LogEntry(
                DateTimeOffset.UtcNow,
                LogLevel.Error,
                nameof(LogChannel.Common),
                string.Empty,
                exception,
                null,
                LogEntrySource.UnityIntercepted);
            _sink(entry);
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            var message = args == null || args.Length == 0
                ? format
                : string.Format(CultureInfo.InvariantCulture, format, args);

            var entry = new LogEntry(
                DateTimeOffset.UtcNow,
                Map(logType),
                nameof(LogChannel.Common),
                message,
                null,
                null,
                LogEntrySource.UnityIntercepted);
            _sink(entry);
        }

        private static LogLevel Map(LogType logType)
        {
            return logType switch
            {
                LogType.Warning => LogLevel.Warning,
                LogType.Error => LogLevel.Error,
                LogType.Exception => LogLevel.Critical,
                LogType.Assert => LogLevel.Error,
                _ => LogLevel.Information
            };
        }
    }
}
