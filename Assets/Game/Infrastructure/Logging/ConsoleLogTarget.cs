using System;
using UnityEngine;

namespace Game.Logging
{
    public interface IUnityConsoleSink
    {
        void Log(LogType type, string message);
    }

    public sealed class UnityConsoleSink : IUnityConsoleSink
    {
        private readonly ILogHandler _handler;

        public UnityConsoleSink(ILogHandler handler)
        {
            _handler = handler ?? Debug.unityLogger.logHandler;
        }

        public void Log(LogType type, string message)
        {
            _handler.LogFormat(type, null, "{0}", message);
        }
    }

    public sealed class ConsoleLogTarget : ILoggerTarget
    {
        private readonly LoggerSettingsService _settings;
        private readonly IUnityConsoleSink _sink;

        public ConsoleLogTarget(LoggerSettingsService settings, IUnityConsoleSink sink)
        {
            _settings = settings;
            _sink = sink;
        }

        public string Name => "Console";

        public void Write(in LogEntry entry)
        {
            if (!_settings.IsConsoleEnabled || entry.Level < _settings.ConsoleMinimumLevel)
            {
                return;
            }

            var message = entry.Source == LogEntrySource.UnityIntercepted
                ? BuildUnityInterceptedMessage(entry)
                : BuildChannelMessage(entry);

            _sink.Log(entry.ToUnityLogType(), message);
        }

        private static string BuildChannelMessage(in LogEntry entry)
        {
            var prefix = string.IsNullOrWhiteSpace(entry.Channel) ? "[Common]" : $"[{entry.Channel}]";
            if (entry.Exception == null)
            {
                return $"{prefix} {entry.Message}";
            }

            if (string.IsNullOrWhiteSpace(entry.Message))
            {
                return $"{prefix} {entry.Exception}";
            }

            return $"{prefix} {entry.Message}\n{entry.Exception}";
        }

        private static string BuildUnityInterceptedMessage(in LogEntry entry)
        {
            return entry.Exception == null
                ? entry.Message
                : string.IsNullOrWhiteSpace(entry.Message)
                    ? entry.Exception.ToString()
                    : $"{entry.Message}\n{entry.Exception}";
        }
    }
}
