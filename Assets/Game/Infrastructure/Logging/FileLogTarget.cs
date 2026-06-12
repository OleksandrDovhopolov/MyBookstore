using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Logging
{
    public sealed class FileLogTarget : ILoggerTarget, IDisposable
    {
        private readonly LoggerSettingsService _settings;
        private readonly string _directory;
        private readonly string _sessionStamp;
        private readonly object _sync = new();

        private StreamWriter _writer;
        private string _currentFilePath;
        private int _fileIndex;

        public FileLogTarget(LoggerSettingsService settings, string baseDirectory = null)
        {
            _settings = settings;
            _directory = string.IsNullOrWhiteSpace(baseDirectory)
                ? Path.Combine(Application.persistentDataPath, "logs")
                : baseDirectory;
            _sessionStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        public string Name => "File";
        public string LogDirectory => _directory;
        public string CurrentFilePath => _currentFilePath;

        public void Write(in LogEntry entry)
        {
            if (!_settings.IsFileEnabled || entry.Level < _settings.FileMinimumLevel)
            {
                return;
            }

            lock (_sync)
            {
                EnsureWriter();
                RotateIfNeeded();
                _writer.WriteLine(FormatLine(entry));
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        private void EnsureWriter()
        {
            if (_writer != null)
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            CleanupOldFiles();
            _currentFilePath = BuildFilePath(_fileIndex);
            _writer = new StreamWriter(new FileStream(_currentFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = true
            };
        }

        private void RotateIfNeeded()
        {
            if (_writer == null)
            {
                return;
            }

            var maxBytes = _settings.RawSettings.RollSizeKb * 1024L;
            if (_writer.BaseStream.Length < maxBytes)
            {
                return;
            }

            _writer.Dispose();
            _writer = null;
            _fileIndex++;
            CleanupOldFiles();
            EnsureWriter();
        }

        private void CleanupOldFiles()
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            var prefix = _settings.RawSettings.FilePrefix + "_";
            var files = new DirectoryInfo(_directory)
                .GetFiles($"{prefix}*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToArray();

            for (var i = _settings.RawSettings.MaxRetainedFiles; i < files.Length; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"[FileLogTarget] Failed to delete old log '{files[i].Name}': {ex.Message}");
                }
            }
        }

        private string BuildFilePath(int index)
        {
            var suffix = index <= 0 ? string.Empty : $"_{index:000}";
            return Path.Combine(_directory, $"{_settings.RawSettings.FilePrefix}_{_sessionStamp}{suffix}.log");
        }

        private static string FormatLine(in LogEntry entry)
        {
            var builder = new StringBuilder(256);
            builder.Append(entry.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            builder.Append(" [");
            builder.Append(entry.Level);
            builder.Append("] [");
            builder.Append(string.IsNullOrWhiteSpace(entry.Channel) ? "Common" : entry.Channel);
            builder.Append("] ");
            builder.Append(Sanitize(entry.Message));

            if (entry.Payload != null)
            {
                builder.Append(" | payload=");
                builder.Append(Sanitize(entry.Payload.ToString()));
            }

            if (entry.Exception != null)
            {
                builder.Append(" | exception=");
                builder.Append(Sanitize(entry.Exception.ToString()));
            }

            return builder.ToString();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
