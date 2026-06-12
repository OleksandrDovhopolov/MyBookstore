using UnityEngine;

namespace Game.Logging
{
    public sealed class LoggerSettingsService : ILoggerSettingsService
    {
        public const string ConsoleEnabledKey = "MyBookstore.Logging.Console.Enabled";
        public const string FileEnabledKey = "MyBookstore.Logging.File.Enabled";
        public const string ConsoleMinimumLevelKey = "MyBookstore.Logging.Console.MinimumLevel";
        public const string FileMinimumLevelKey = "MyBookstore.Logging.File.MinimumLevel";

        private readonly LoggerSettings _settings;

        public LoggerSettingsService(LoggerSettings settings)
        {
            _settings = settings != null ? settings : CreateDefaultSettings();
        }

        public bool IsConsoleEnabled => ReadBool(ConsoleEnabledKey, _settings.GetDefaultConsoleEnabled());
        public bool IsFileEnabled => ReadBool(FileEnabledKey, _settings.GetDefaultFileEnabled());
        public LogLevel ConsoleMinimumLevel => ReadLevel(ConsoleMinimumLevelKey, _settings.GetDefaultConsoleMinimumLevel());
        public LogLevel FileMinimumLevel => ReadLevel(FileMinimumLevelKey, _settings.GetDefaultFileMinimumLevel());

        public bool IsEnabledForAnyTarget(LogLevel level)
        {
            return (IsConsoleEnabled && level >= ConsoleMinimumLevel)
                   || (IsFileEnabled && level >= FileMinimumLevel);
        }

        public void SetConsoleEnabledOverride(bool enabled)
        {
            PlayerPrefs.SetInt(ConsoleEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetFileEnabledOverride(bool enabled)
        {
            PlayerPrefs.SetInt(FileEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ClearConsoleEnabledOverride()
        {
            PlayerPrefs.DeleteKey(ConsoleEnabledKey);
            PlayerPrefs.Save();
        }

        public void ClearFileEnabledOverride()
        {
            PlayerPrefs.DeleteKey(FileEnabledKey);
            PlayerPrefs.Save();
        }

        public void SetConsoleMinimumLevelOverride(LogLevel level)
        {
            PlayerPrefs.SetInt(ConsoleMinimumLevelKey, (int)level);
            PlayerPrefs.Save();
        }

        public void SetFileMinimumLevelOverride(LogLevel level)
        {
            PlayerPrefs.SetInt(FileMinimumLevelKey, (int)level);
            PlayerPrefs.Save();
        }

        public void ClearMinimumLevelOverrides()
        {
            PlayerPrefs.DeleteKey(ConsoleMinimumLevelKey);
            PlayerPrefs.DeleteKey(FileMinimumLevelKey);
            PlayerPrefs.Save();
        }

        internal LoggerSettings RawSettings => _settings;

        private static bool ReadBool(string key, bool defaultValue)
        {
            return !PlayerPrefs.HasKey(key) ? defaultValue : PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        private static LogLevel ReadLevel(string key, LogLevel defaultValue)
        {
            return !PlayerPrefs.HasKey(key) ? defaultValue : (LogLevel)PlayerPrefs.GetInt(key, (int)defaultValue);
        }

        private static LoggerSettings CreateDefaultSettings()
        {
            var settings = ScriptableObject.CreateInstance<LoggerSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }
    }
}
