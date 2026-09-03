using System;
using Game.AdminTooling.Editor;
using Newtonsoft.Json.Linq;

namespace Save.Editor
{
    /// <summary>
    /// Pure request/response shaping for the admin player-save-reset endpoint. Kept free of
    /// UnityWebRequest and EditorGUI so it can be unit tested.
    /// </summary>
    public static class PlayerSaveResetRequest
    {
        /// <summary>Confirmation token the backend requires in the body; a typo here is a silent no-op.</summary>
        public const string ConfirmToken = "RESET_PLAYER_SAVE";

        public static string BuildUrl(string baseUrl, string playerId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            var root = AdminApiSettings.NormalizeBaseUrl(baseUrl);
            var escaped = Uri.EscapeDataString(playerId.Trim());
            return $"{root}/api/admin/test/player/{escaped}/save/reset";
        }

        /// <summary>
        /// GET /api/admin/player/{id} — read-only, used by Test Connection to validate credentials
        /// against the same Basic-auth realm without mutating anything.
        /// </summary>
        public static string BuildPlayerUrl(string baseUrl, string playerId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id is required.", nameof(playerId));
            }

            var root = AdminApiSettings.NormalizeBaseUrl(baseUrl);
            var escaped = Uri.EscapeDataString(playerId.Trim());
            return $"{root}/api/admin/player/{escaped}";
        }

        public static string BuildBody() => "{\"confirm\":\"" + ConfirmToken + "\"}";

        public sealed class Response
        {
            public bool Success;
            public string ErrorCode;
            public string ErrorMessage;
        }

        /// <summary>
        /// Tolerant parse: the endpoint may answer with a bare body or an error envelope, and a
        /// non-JSON body (proxy HTML, empty 500) must not throw — the caller falls back to raw text.
        /// </summary>
        public static bool TryParseResponse(string json, out Response response)
        {
            response = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception)
            {
                return false;
            }

            response = new Response
            {
                Success = root.Value<bool?>("success") ?? false,
                ErrorCode = root.Value<string>("errorCode"),
                ErrorMessage = root.Value<string>("errorMessage")
            };
            return true;
        }
    }
}
