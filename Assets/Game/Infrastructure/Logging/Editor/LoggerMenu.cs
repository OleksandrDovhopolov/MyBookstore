#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Logging.Editor
{
    public static class LoggerMenu
    {
        private const string EnableFileMenuPath = "Tools/Logging/Enable File Logging Override";
        private const string DisableFileMenuPath = "Tools/Logging/Disable File Logging Override";
        private const string ClearFileMenuPath = "Tools/Logging/Clear File Logging Override";
        private const string PrintDirMenuPath = "Tools/Logging/Print Log Directory";

        private const string ConsoleLevelRoot = "Tools/Logging/Console Min Level/";
        private const string ClearLevelsMenuPath = "Tools/Logging/Clear Level Overrides";

        // ----- File logging override -----

        [MenuItem(EnableFileMenuPath)]
        private static void EnableFileLogging()
        {
            PlayerPrefs.SetInt(LoggerSettingsService.FileEnabledKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[Logging] File logging override enabled.");
        }

        [MenuItem(DisableFileMenuPath)]
        private static void DisableFileLogging()
        {
            PlayerPrefs.SetInt(LoggerSettingsService.FileEnabledKey, 0);
            PlayerPrefs.Save();
            Debug.Log("[Logging] File logging override disabled.");
        }

        [MenuItem(ClearFileMenuPath)]
        private static void ClearFileLoggingOverride()
        {
            PlayerPrefs.DeleteKey(LoggerSettingsService.FileEnabledKey);
            PlayerPrefs.Save();
            Debug.Log("[Logging] File logging override cleared.");
        }

        [MenuItem(PrintDirMenuPath)]
        private static void PrintLogDirectory()
        {
            var dir = System.IO.Path.Combine(Application.persistentDataPath, "logs");
            Debug.Log($"[Logging] Log directory: {dir}");
        }

        // ----- Console minimum level override (PlayerPrefs — beats the LoggerSettings asset default) -----
        // Set to Information to hide Debug spam (e.g. loading "result=start" lines) while keeping Info+.

        [MenuItem(ConsoleLevelRoot + "Trace")] private static void ConsoleTrace() => SetConsoleLevel(LogLevel.Trace);
        [MenuItem(ConsoleLevelRoot + "Trace", true)] private static bool ConsoleTraceCheck() => Check(ConsoleLevelRoot + "Trace", LogLevel.Trace);

        [MenuItem(ConsoleLevelRoot + "Debug")] private static void ConsoleDebug() => SetConsoleLevel(LogLevel.Debug);
        [MenuItem(ConsoleLevelRoot + "Debug", true)] private static bool ConsoleDebugCheck() => Check(ConsoleLevelRoot + "Debug", LogLevel.Debug);

        [MenuItem(ConsoleLevelRoot + "Information")] private static void ConsoleInformation() => SetConsoleLevel(LogLevel.Information);
        [MenuItem(ConsoleLevelRoot + "Information", true)] private static bool ConsoleInformationCheck() => Check(ConsoleLevelRoot + "Information", LogLevel.Information);

        [MenuItem(ConsoleLevelRoot + "Warning")] private static void ConsoleWarning() => SetConsoleLevel(LogLevel.Warning);
        [MenuItem(ConsoleLevelRoot + "Warning", true)] private static bool ConsoleWarningCheck() => Check(ConsoleLevelRoot + "Warning", LogLevel.Warning);

        [MenuItem(ConsoleLevelRoot + "Error")] private static void ConsoleError() => SetConsoleLevel(LogLevel.Error);
        [MenuItem(ConsoleLevelRoot + "Error", true)] private static bool ConsoleErrorCheck() => Check(ConsoleLevelRoot + "Error", LogLevel.Error);

        [MenuItem(ClearLevelsMenuPath)]
        private static void ClearLevelOverrides()
        {
            PlayerPrefs.DeleteKey(LoggerSettingsService.ConsoleMinimumLevelKey);
            PlayerPrefs.DeleteKey(LoggerSettingsService.FileMinimumLevelKey);
            PlayerPrefs.Save();
            Debug.Log("[Logging] Level overrides cleared (back to LoggerSettings defaults).");
        }

        private static void SetConsoleLevel(LogLevel level)
        {
            PlayerPrefs.SetInt(LoggerSettingsService.ConsoleMinimumLevelKey, (int)level);
            PlayerPrefs.Save();
            Debug.Log($"[Logging] Console min level override = {level}.");
        }

        private static bool Check(string menuPath, LogLevel level)
        {
            var isCurrent = PlayerPrefs.HasKey(LoggerSettingsService.ConsoleMinimumLevelKey)
                            && PlayerPrefs.GetInt(LoggerSettingsService.ConsoleMinimumLevelKey) == (int)level;
            Menu.SetChecked(menuPath, isCurrent);
            return true;
        }
    }
}
#endif
