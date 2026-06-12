using UnityEngine;

namespace Game.Logging
{
    [CreateAssetMenu(fileName = "LoggerSettings", menuName = "Game/Logging/LoggerSettings")]
    public sealed class LoggerSettings : ScriptableObject
    {
        [Header("Editor Defaults")]
        [Tooltip("Defaults applied in the Unity Editor (Application.isEditor). Used when there is no runtime PlayerPrefs override (e.g. set via the Tools/Logging menu).")]
        [SerializeField] private bool _editorConsoleEnabled = true;
        [SerializeField] private bool _editorFileEnabled = false;
        [SerializeField] private LogLevel _editorConsoleMinimumLevel = LogLevel.Debug;
        [SerializeField] private LogLevel _editorFileMinimumLevel = LogLevel.Debug;

        [Header("Player Defaults")]
        [Tooltip("Defaults applied in player / device builds. Used when there is no runtime PlayerPrefs override.")]
        [SerializeField] private bool _playerConsoleEnabled = true;
        [SerializeField] private bool _playerFileEnabled = true;
        [SerializeField] private LogLevel _playerConsoleMinimumLevel = LogLevel.Information;
        [SerializeField] private LogLevel _playerFileMinimumLevel = LogLevel.Debug;

        [Header("File Settings")]
        [SerializeField] private string _filePrefix = "mybookstore";
        [SerializeField] private int _rollSizeKb = 1024;
        [SerializeField] private int _maxRetainedFiles = 10;

        public bool GetDefaultConsoleEnabled() => Application.isEditor ? _editorConsoleEnabled : _playerConsoleEnabled;
        public bool GetDefaultFileEnabled() => Application.isEditor ? _editorFileEnabled : _playerFileEnabled;
        public LogLevel GetDefaultConsoleMinimumLevel() => Application.isEditor ? _editorConsoleMinimumLevel : _playerConsoleMinimumLevel;
        public LogLevel GetDefaultFileMinimumLevel() => Application.isEditor ? _editorFileMinimumLevel : _playerFileMinimumLevel;

        public string FilePrefix => string.IsNullOrWhiteSpace(_filePrefix) ? "mybookstore" : _filePrefix;
        public int RollSizeKb => _rollSizeKb <= 0 ? 1024 : _rollSizeKb;
        public int MaxRetainedFiles => _maxRetainedFiles <= 0 ? 10 : _maxRetainedFiles;
    }
}
