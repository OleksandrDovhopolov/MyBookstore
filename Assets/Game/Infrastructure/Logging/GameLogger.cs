using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Logging
{
    internal sealed class ChannelLogger<TChannel> : IChannelLogger<TChannel>
    {
        private readonly GameLogger _owner;
        private readonly string _channelName;

        public ChannelLogger(GameLogger owner)
        {
            _owner = owner;
            _channelName = typeof(TChannel).Name;
        }

        public bool IsEnabled(LogLevel level) => _owner.IsEnabled(level);

        public void Log(LogLevel level, string message, Exception exception = null, object payload = null)
        {
            _owner.WriteChannel(level, _channelName, message, exception, payload);
        }
    }

    internal sealed class CommonChannelLogger : IChannelLogger
    {
        private readonly GameLogger _owner;

        public CommonChannelLogger(GameLogger owner)
        {
            _owner = owner;
        }

        public bool IsEnabled(LogLevel level) => _owner.IsEnabled(level);

        public void Log(LogLevel level, string message, Exception exception = null, object payload = null)
        {
            _owner.WriteChannel(level, nameof(LogChannel.Common), message, exception, payload);
        }
    }

    public sealed class GameLogger : ILogService
    {
        private readonly LoggerSettingsService _settings;
        private readonly FileLogTarget _fileTarget;
        private readonly ConsoleLogTarget _consoleTarget;
        private readonly Dictionary<Type, IChannelLogger> _cache = new();
        private readonly ILogHandler _previousHandler;
        private readonly GameLogHandler _handler;
        private readonly IChannelLogger _commonLogger;

        public GameLogger(LoggerSettingsService settings, string fileBaseDirectory = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _previousHandler = Debug.unityLogger.logHandler;
            _fileTarget = new FileLogTarget(_settings, fileBaseDirectory);
            _consoleTarget = new ConsoleLogTarget(_settings, new UnityConsoleSink(_previousHandler));
            _handler = new GameLogHandler(_previousHandler, WriteIntercepted);
            _commonLogger = new CommonChannelLogger(this);

            Debug.unityLogger.logHandler = _handler;
        }

        public IChannelLogger<TChannel> GetLogger<TChannel>()
        {
            var key = typeof(TChannel);
            if (_cache.TryGetValue(key, out var existing))
            {
                return (IChannelLogger<TChannel>)existing;
            }

            var created = new ChannelLogger<TChannel>(this);
            _cache[key] = created;
            return created;
        }

        public IChannelLogger GetCommonLogger() => _commonLogger;

        public string GetCurrentLogDirectory() => _fileTarget.LogDirectory;

        public string GetCurrentLogFilePath() => _fileTarget.CurrentFilePath;

        public void Dispose()
        {
            if (ReferenceEquals(Debug.unityLogger.logHandler, _handler))
            {
                Debug.unityLogger.logHandler = _previousHandler;
            }

            _fileTarget.Dispose();
            _cache.Clear();
        }

        internal bool IsEnabled(LogLevel level) => _settings.IsEnabledForAnyTarget(level);

        internal void WriteChannel(LogLevel level, string channelName, string message, Exception exception, object payload)
        {
            var entry = new LogEntry(
                DateTimeOffset.UtcNow,
                level,
                string.IsNullOrWhiteSpace(channelName) ? nameof(LogChannel.Common) : channelName,
                message ?? string.Empty,
                exception,
                payload,
                LogEntrySource.ChannelLogger);

            Dispatch(entry);
        }

        private void WriteIntercepted(LogEntry entry)
        {
            Dispatch(entry);
        }

        private void Dispatch(in LogEntry entry)
        {
            _fileTarget.Write(entry);
            _consoleTarget.Write(entry);
        }
    }
}
