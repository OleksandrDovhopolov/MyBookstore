using System;
using System.IO;
using Game.Commands;
using NUnit.Framework;
using UnityEngine;

namespace Game.Logging.Tests.Editor
{
    public sealed class LoggingModuleTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(LoggerSettingsService.ConsoleEnabledKey);
            PlayerPrefs.DeleteKey(LoggerSettingsService.FileEnabledKey);
            PlayerPrefs.DeleteKey(LoggerSettingsService.ConsoleMinimumLevelKey);
            PlayerPrefs.DeleteKey(LoggerSettingsService.FileMinimumLevelKey);
            PlayerPrefs.Save();

            _tempDirectory = Path.Combine(Path.GetTempPath(), "MyBookstoreLoggingTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
            }

            PlayerPrefs.DeleteKey(LoggerSettingsService.ConsoleEnabledKey);
            PlayerPrefs.DeleteKey(LoggerSettingsService.FileEnabledKey);
            PlayerPrefs.DeleteKey(LoggerSettingsService.ConsoleMinimumLevelKey);
            PlayerPrefs.DeleteKey(LoggerSettingsService.FileMinimumLevelKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void LoggerSettingsService_Uses_PlayerPrefs_Overrides()
        {
            var asset = ScriptableObject.CreateInstance<LoggerSettings>();
            var service = new LoggerSettingsService(asset);

            service.SetFileEnabledOverride(true);
            service.SetConsoleEnabledOverride(false);
            service.SetFileMinimumLevelOverride(LogLevel.Warning);

            Assert.That(service.IsFileEnabled, Is.True);
            Assert.That(service.IsConsoleEnabled, Is.False);
            Assert.That(service.FileMinimumLevel, Is.EqualTo(LogLevel.Warning));
        }

        [Test]
        public void FileLogTarget_Writes_Log_File()
        {
            var asset = ScriptableObject.CreateInstance<LoggerSettings>();
            var settings = new LoggerSettingsService(asset);
            settings.SetFileEnabledOverride(true);
            settings.SetFileMinimumLevelOverride(LogLevel.Debug);

            using var target = new FileLogTarget(settings, _tempDirectory);
            target.Write(new LogEntry(
                DateTimeOffset.UtcNow,
                LogLevel.Information,
                "Gameplay",
                "Player opened shop",
                null,
                null,
                LogEntrySource.ChannelLogger));

            Assert.That(target.CurrentFilePath, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(target.CurrentFilePath), Is.True);

            var text = File.ReadAllText(target.CurrentFilePath);
            Assert.That(text, Does.Contain("Player opened shop"));
            Assert.That(text, Does.Contain("[Information] [Gameplay]"));
        }

        [Test]
        public void FileLogTarget_Rolls_When_Size_Limit_Is_Reached()
        {
            var asset = ScriptableObject.CreateInstance<LoggerSettings>();
            typeof(LoggerSettings).GetField("_rollSizeKb", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(asset, 1);

            var settings = new LoggerSettingsService(asset);
            settings.SetFileEnabledOverride(true);
            settings.SetFileMinimumLevelOverride(LogLevel.Debug);

            using var target = new FileLogTarget(settings, _tempDirectory);
            for (var i = 0; i < 80; i++)
            {
                target.Write(new LogEntry(
                    DateTimeOffset.UtcNow,
                    LogLevel.Information,
                    "Gameplay",
                    new string('x', 80),
                    null,
                    null,
                    LogEntrySource.ChannelLogger));
            }

            var files = Directory.GetFiles(_tempDirectory, "*.log");
            Assert.That(files.Length, Is.GreaterThan(1));
        }

        [Test]
        public void CommandLoggerAdapter_Maps_To_Logging_Module()
        {
            var channelLogger = new FakeChannelLogger();
            var logService = new FakeLogService(channelLogger);
            var adapter = new CommandLoggerAdapter(logService);

            adapter.Log(CommandLogLevel.Warning, "warn-message");
            adapter.LogException(new InvalidOperationException("boom"), "cmd-failed");

            Assert.That(channelLogger.LastLevel, Is.EqualTo(LogLevel.Error));
            Assert.That(channelLogger.LastMessage, Is.EqualTo("cmd-failed"));
            Assert.That(channelLogger.LastException, Is.TypeOf<InvalidOperationException>());
            Assert.That(channelLogger.Messages, Does.Contain("warn-message"));
        }

        [Test]
        public void GameLogger_Caches_Typed_Loggers()
        {
            var asset = ScriptableObject.CreateInstance<LoggerSettings>();
            var settings = new LoggerSettingsService(asset);
            settings.SetFileEnabledOverride(false);

            using var logger = new GameLogger(settings, _tempDirectory);
            var first = logger.GetLogger<LogChannel.Gameplay>();
            var second = logger.GetLogger<LogChannel.Gameplay>();

            Assert.That(ReferenceEquals(first, second), Is.True);
        }

        private sealed class FakeLogService : ILogService
        {
            private readonly IChannelLogger<LogChannel.Infrastructure> _logger;

            public FakeLogService(IChannelLogger<LogChannel.Infrastructure> logger)
            {
                _logger = logger;
            }

            public IChannelLogger<TChannel> GetLogger<TChannel>()
            {
                return (IChannelLogger<TChannel>)_logger;
            }

            public IChannelLogger GetCommonLogger() => _logger;
            public string GetCurrentLogDirectory() => string.Empty;
            public string GetCurrentLogFilePath() => string.Empty;
            public void Dispose() { }
        }

        private sealed class FakeChannelLogger : IChannelLogger<LogChannel.Infrastructure>
        {
            public readonly System.Collections.Generic.List<string> Messages = new();
            public LogLevel LastLevel { get; private set; }
            public string LastMessage { get; private set; }
            public Exception LastException { get; private set; }

            public bool IsEnabled(LogLevel level) => true;

            public void Log(LogLevel level, string message, Exception exception = null, object payload = null)
            {
                LastLevel = level;
                LastMessage = message;
                LastException = exception;
                Messages.Add(message);
            }
        }
    }
}
