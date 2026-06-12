using System;
using Game.Commands;

namespace Game.Logging
{
    public sealed class CommandLoggerAdapter : ICommandLogger
    {
        private readonly IChannelLogger<LogChannel.Infrastructure> _logger;

        public CommandLoggerAdapter(ILogService logService)
        {
            if (logService == null) throw new ArgumentNullException(nameof(logService));
            _logger = logService.GetLogger<LogChannel.Infrastructure>();
        }

        public void Log(CommandLogLevel level, string message)
        {
            _logger.Log(Map(level), message ?? string.Empty);
        }

        public void LogException(Exception exception, string message = null)
        {
            _logger.Log(LogLevel.Error, message ?? string.Empty, exception);
        }

        private static LogLevel Map(CommandLogLevel level)
        {
            return level switch
            {
                CommandLogLevel.Trace => LogLevel.Trace,
                CommandLogLevel.Debug => LogLevel.Debug,
                CommandLogLevel.Warning => LogLevel.Warning,
                CommandLogLevel.Error => LogLevel.Error,
                _ => LogLevel.Information
            };
        }
    }
}
