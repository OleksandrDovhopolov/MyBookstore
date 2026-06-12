using System;
using UnityEngine;

namespace Game.Logging
{
    public enum LogEntrySource
    {
        ChannelLogger = 0,
        UnityIntercepted = 1
    }

    public readonly struct LogEntry
    {
        public LogEntry(
            DateTimeOffset timestampUtc,
            LogLevel level,
            string channel,
            string message,
            Exception exception,
            object payload,
            LogEntrySource source)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Channel = channel;
            Message = message;
            Exception = exception;
            Payload = payload;
            Source = source;
        }

        public DateTimeOffset TimestampUtc { get; }
        public LogLevel Level { get; }
        public string Channel { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public object Payload { get; }
        public LogEntrySource Source { get; }

        public LogType ToUnityLogType()
        {
            return Level switch
            {
                LogLevel.Warning => LogType.Warning,
                LogLevel.Error => LogType.Error,
                LogLevel.Critical => LogType.Exception,
                _ => LogType.Log
            };
        }
    }
}
