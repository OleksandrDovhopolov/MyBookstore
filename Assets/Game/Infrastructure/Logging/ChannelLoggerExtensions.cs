using System;
using System.Diagnostics;
using System.Globalization;

namespace Game.Logging
{
    public static class ChannelLoggerExtensions
    {
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogTrace(this IChannelLogger logger, string message)
        {
            logger?.Log(LogLevel.Trace, message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogTrace(this IChannelLogger logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Trace, null, null, format, args);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogTrace<TChannel>(this IChannelLogger<TChannel> logger, string message)
        {
            logger?.Log(LogLevel.Trace, message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogTrace<TChannel>(this IChannelLogger<TChannel> logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Trace, null, null, format, args);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogDebug(this IChannelLogger logger, string message)
        {
            logger?.Log(LogLevel.Debug, message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogDebug(this IChannelLogger logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Debug, null, null, format, args);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogDebug<TChannel>(this IChannelLogger<TChannel> logger, string message)
        {
            logger?.Log(LogLevel.Debug, message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogDebug<TChannel>(this IChannelLogger<TChannel> logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Debug, null, null, format, args);
        }

        public static void LogInformation<TChannel>(this IChannelLogger<TChannel> logger, string message)
        {
            logger?.Log(LogLevel.Information, message);
        }

        public static void LogInformation(this IChannelLogger logger, string message)
        {
            logger?.Log(LogLevel.Information, message);
        }

        public static void LogInformation(this IChannelLogger logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Information, null, null, format, args);
        }

        public static void LogInformation(this IChannelLogger logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Information, message, exception);
        }

        public static void LogInformation<TChannel>(this IChannelLogger<TChannel> logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Information, null, null, format, args);
        }

        public static void LogInformation<TChannel>(this IChannelLogger<TChannel> logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Information, message, exception);
        }

        public static void LogWarning(this IChannelLogger logger, string message)
        {
            logger?.Log(LogLevel.Warning, message);
        }

        public static void LogWarning(this IChannelLogger logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Warning, null, null, format, args);
        }

        public static void LogWarning<TChannel>(this IChannelLogger<TChannel> logger, string message)
        {
            logger?.Log(LogLevel.Warning, message);
        }

        public static void LogWarning<TChannel>(this IChannelLogger<TChannel> logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Warning, null, null, format, args);
        }

        public static void LogWarning(this IChannelLogger logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Warning, message, exception);
        }

        public static void LogWarning<TChannel>(this IChannelLogger<TChannel> logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Warning, message, exception);
        }

        public static void LogWarning(this IChannelLogger logger, Exception exception, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Warning, exception, null, format, args);
        }

        public static void LogWarning<TChannel>(this IChannelLogger<TChannel> logger, Exception exception, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Warning, exception, null, format, args);
        }

        public static void LogError(this IChannelLogger logger, string message)
        {
            logger?.Log(LogLevel.Error, message);
        }

        public static void LogError(this IChannelLogger logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Error, null, null, format, args);
        }

        public static void LogError<TChannel>(this IChannelLogger<TChannel> logger, string message)
        {
            logger?.Log(LogLevel.Error, message);
        }

        public static void LogError<TChannel>(this IChannelLogger<TChannel> logger, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Error, null, null, format, args);
        }

        public static void LogError(this IChannelLogger logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Error, message, exception);
        }

        public static void LogError<TChannel>(this IChannelLogger<TChannel> logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Error, message, exception);
        }

        public static void LogError(this IChannelLogger logger, Exception exception, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Error, exception, null, format, args);
        }

        public static void LogError<TChannel>(this IChannelLogger<TChannel> logger, Exception exception, string format, params object[] args)
        {
            LogFormatted(logger, LogLevel.Error, exception, null, format, args);
        }

        public static void LogCritical(this IChannelLogger logger, string message)
        {
            logger?.Log(LogLevel.Critical, message);
        }

        public static void LogCritical<TChannel>(this IChannelLogger<TChannel> logger, string message)
        {
            logger?.Log(LogLevel.Critical, message);
        }

        public static void LogCritical(this IChannelLogger logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Critical, message, exception);
        }

        public static void LogCritical<TChannel>(this IChannelLogger<TChannel> logger, Exception exception, string message)
        {
            logger?.Log(LogLevel.Critical, message, exception);
        }

        public static void LogInformationWithPayload(this IChannelLogger logger, object payload, string message)
        {
            logger?.Log(LogLevel.Information, message, null, payload);
        }

        public static void LogInformationWithPayload<TChannel>(this IChannelLogger<TChannel> logger, object payload, string message)
        {
            logger?.Log(LogLevel.Information, message, null, payload);
        }

        public static void LogWarningWithPayload(this IChannelLogger logger, object payload, string message)
        {
            logger?.Log(LogLevel.Warning, message, null, payload);
        }

        public static void LogWarningWithPayload<TChannel>(this IChannelLogger<TChannel> logger, object payload, string message)
        {
            logger?.Log(LogLevel.Warning, message, null, payload);
        }

        public static void LogErrorWithPayload(this IChannelLogger logger, object payload, string message)
        {
            logger?.Log(LogLevel.Error, message, null, payload);
        }

        public static void LogErrorWithPayload<TChannel>(this IChannelLogger<TChannel> logger, object payload, string message)
        {
            logger?.Log(LogLevel.Error, message, null, payload);
        }

        private static void LogFormatted(
            IChannelLogger logger,
            LogLevel level,
            Exception exception,
            object payload,
            string format,
            object[] args)
        {
            if (logger == null || !logger.IsEnabled(level))
            {
                return;
            }

            logger.Log(level, SafeFormat(format, args), exception, payload);
        }

        private static void LogFormatted<TChannel>(
            IChannelLogger<TChannel> logger,
            LogLevel level,
            Exception exception,
            object payload,
            string format,
            object[] args)
        {
            if (logger == null || !logger.IsEnabled(level))
            {
                return;
            }

            logger.Log(level, SafeFormat(format, args), exception, payload);
        }

        private static string SafeFormat(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
