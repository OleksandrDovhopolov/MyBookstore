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
    }
}
#endif
