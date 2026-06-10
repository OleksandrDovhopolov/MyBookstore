using System;

namespace Game.Logging
{
    public interface IChannelLogger
    {
        bool IsEnabled(LogLevel level);
        void Log(LogLevel level, string message, Exception exception = null, object payload = null);
    }

    public interface IChannelLogger<TChannel> : IChannelLogger
    {
    }

    public interface ILogService : IDisposable
    {
        IChannelLogger<TChannel> GetLogger<TChannel>();
        IChannelLogger GetCommonLogger();
        string GetCurrentLogDirectory();
        string GetCurrentLogFilePath();
    }

    public interface ILoggerSettingsService
    {
        bool IsConsoleEnabled { get; }
        bool IsFileEnabled { get; }
        LogLevel ConsoleMinimumLevel { get; }
        LogLevel FileMinimumLevel { get; }

        bool IsEnabledForAnyTarget(LogLevel level);
        void SetConsoleEnabledOverride(bool enabled);
        void SetFileEnabledOverride(bool enabled);
        void ClearConsoleEnabledOverride();
        void ClearFileEnabledOverride();
        void SetConsoleMinimumLevelOverride(LogLevel level);
        void SetFileMinimumLevelOverride(LogLevel level);
        void ClearMinimumLevelOverrides();
    }
}
