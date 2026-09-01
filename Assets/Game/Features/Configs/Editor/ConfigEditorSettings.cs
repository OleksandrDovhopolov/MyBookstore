using Game.AdminTooling.Editor;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Креденшалы и base URL для admin API (§7 спеки).
    ///
    /// Значения переехали в общий <see cref="AdminApiSettings"/>, чтобы Config Editor и
    /// Player Save Reset window читали одни и те же EditorPrefs. Этот класс оставлен тонким
    /// форвардером, чтобы не трогать существующие call-sites.
    /// </summary>
    internal static class ConfigEditorSettings
    {
        public const string DefaultBaseUrl = AdminApiSettings.DefaultBaseUrl;

        public static string BaseUrl
        {
            get => AdminApiSettings.BaseUrl;
            set => AdminApiSettings.BaseUrl = value;
        }

        public static string Username
        {
            get => AdminApiSettings.Username;
            set => AdminApiSettings.Username = value;
        }

        public static string Password
        {
            get => AdminApiSettings.Password;
            set => AdminApiSettings.Password = value;
        }

        public static bool IsConfigured => AdminApiSettings.IsConfigured;
    }
}
