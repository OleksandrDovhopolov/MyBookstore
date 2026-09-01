using System;
using System.Text;
using UnityEditor;

namespace Game.AdminTooling.Editor
{
    /// <summary>
    /// Shared admin API connection settings for every editor tool that talks to the game server
    /// (Config Editor, Player Save Reset, ...). EditorPrefs — per-machine, never committed.
    ///
    /// The EditorPrefs keys keep their original "MyBookstore.Configs.*" names on purpose: they were
    /// introduced by the Config Editor, and renaming them would silently drop credentials that people
    /// already have saved locally.
    /// </summary>
    public static class AdminApiSettings
    {
        private const string BaseUrlKey = "MyBookstore.Configs.AdminBaseUrl";
        private const string UserKey = "MyBookstore.Configs.AdminUser";
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

        /// <summary>Trailing-slash-free base URL, safe to concatenate with an absolute path.</summary>
        public static string NormalizeBaseUrl(string baseUrl)
            => string.IsNullOrEmpty(baseUrl) ? baseUrl : baseUrl.Trim().TrimEnd('/');

        /// <summary>
        /// "Basic base64(user:pass)", or null when nothing is configured. Never log the result.
        /// </summary>
        public static string BuildBasicAuthHeader(string username, string password)
        {
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            {
                return null;
            }

            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
            return "Basic " + token;
        }

        public static string BuildBasicAuthHeader() => BuildBasicAuthHeader(Username, Password);
    }
}
