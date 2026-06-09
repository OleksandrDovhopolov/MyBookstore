using UnityEditor;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Хранилище креденшалов и base URL для admin API (§7 спеки).
    /// EditorPrefs — per-машина, в репо не комитится.
    /// </summary>
    internal static class ConfigEditorSettings
    {
        private const string BaseUrlKey  = "MyBookstore.Configs.AdminBaseUrl";
        private const string UserKey     = "MyBookstore.Configs.AdminUser";
        private const string PasswordKey = "MyBookstore.Configs.AdminPass";

        public const string DefaultBaseUrl = "https://gameserver-production-be8b.up.railway.app";

        public static string BaseUrl
        {
            get => EditorPrefs.GetString(BaseUrlKey, DefaultBaseUrl);
            set => EditorPrefs.SetString(BaseUrlKey, value ?? string.Empty);
        }

        public static string Username
        {
            get => EditorPrefs.GetString(UserKey, string.Empty);
            set => EditorPrefs.SetString(UserKey, value ?? string.Empty);
        }

        public static string Password
        {
            get => EditorPrefs.GetString(PasswordKey, string.Empty);
            set => EditorPrefs.SetString(PasswordKey, value ?? string.Empty);
        }

        public static bool IsConfigured
            => !string.IsNullOrWhiteSpace(BaseUrl)
               && !string.IsNullOrWhiteSpace(Username)
               && !string.IsNullOrWhiteSpace(Password);
    }
}
