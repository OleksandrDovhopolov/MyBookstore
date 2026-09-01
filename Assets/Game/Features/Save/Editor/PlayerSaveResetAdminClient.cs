using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.AdminTooling.Editor;
using UnityEngine;
using UnityEngine.Networking;

namespace Save.Editor
{
    /// <summary>
    /// Thin admin client for the player save endpoints.
    /// Straight on UnityWebRequest, mirroring Game.Configs.Editor.AdminApiClient. Editor-only.
    /// </summary>
    public sealed class PlayerSaveResetAdminClient
    {
        private const int TimeoutSeconds = 30;

        public sealed class Result
        {
            public bool Success;
            public long StatusCode;
            public string Body;
            public string Error;
            public bool Canceled;
        }

        /// <summary>POST /api/admin/test/player/{playerId}/save/reset — wipes the server-side save.</summary>
        public UniTask<Result> ResetAsync(string playerId, CancellationToken ct)
        {
            var url = PlayerSaveResetRequest.BuildUrl(AdminApiSettings.BaseUrl, playerId);
            return SendAsync(url, UnityWebRequest.kHttpVerbPOST, PlayerSaveResetRequest.BuildBody(), ct);
        }

        /// <summary>
        /// GET /api/admin/player/{id} — read-only probe of the same Basic-auth realm, used to validate
        /// Base URL + credentials without touching any data. 404 still proves auth works.
        /// </summary>
        public UniTask<Result> TestConnectionAsync(string playerId, CancellationToken ct)
        {
            var url = PlayerSaveResetRequest.BuildPlayerUrl(AdminApiSettings.BaseUrl, playerId);
            return SendAsync(url, UnityWebRequest.kHttpVerbGET, null, ct);
        }

        private static async UniTask<Result> SendAsync(string url, string verb, string body, CancellationToken ct)
        {
            // Log verb + URL + status ONLY. Never log the Authorization header, the password or the
            // request/response body. If you add fields here, re-check that nothing secret leaks.
            Debug.Log($"[PlayerSaveReset] {verb} {url}");

            using var req = new UnityWebRequest(url, verb);
            req.timeout = TimeoutSeconds;
            req.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }

            var basic = AdminApiSettings.BuildBasicAuthHeader();
            if (basic != null)
            {
                req.SetRequestHeader("Authorization", basic);
            }

            try
            {
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await UniTask.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                req.Abort();
                return new Result { Canceled = true, Error = "Canceled." };
            }

            var status = req.responseCode;
            var success = status >= 200 && status < 300;
            var responseBody = req.downloadHandler != null ? req.downloadHandler.text : null;

            Debug.Log($"[PlayerSaveReset] {verb} {url} -> {status} ({(success ? "ok" : "error")})");

            return new Result
            {
                Success = success,
                StatusCode = status,
                Body = responseBody,
                Error = success ? null : DescribeError(req, status, responseBody)
            };
        }

        /// <summary>Maps the documented status codes to something actionable in the window.</summary>
        private static string DescribeError(UnityWebRequest req, long status, string body)
        {
            var backend = ExtractBackendMessage(body);

            switch (status)
            {
                case 400:
                    return backend ?? "400 Bad Request — backend rejected the request payload.";
                case 401:
                    return "401 Unauthorized — check Username / Password.";
                case 403:
                    return "403 Forbidden — player save reset is disabled on the server.";
                case 500:
                    return backend ?? Raw(body) ?? "500 Internal Server Error.";
            }

            if (status == 0)
            {
                // No HTTP response at all: DNS failure, refused connection, or the 30 s timeout.
                return string.IsNullOrEmpty(req.error)
                    ? "Network error or timeout — no response from the server."
                    : $"Network error: {req.error}";
            }

            return backend ?? Raw(body) ?? $"HTTP {status}";
        }

        private static string ExtractBackendMessage(string body)
        {
            if (!PlayerSaveResetRequest.TryParseResponse(body, out var parsed))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(parsed.ErrorMessage))
            {
                return string.IsNullOrWhiteSpace(parsed.ErrorCode)
                    ? parsed.ErrorMessage
                    : $"{parsed.ErrorMessage} ({parsed.ErrorCode})";
            }

            return string.IsNullOrWhiteSpace(parsed.ErrorCode) ? null : parsed.ErrorCode;
        }

        private static string Raw(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return body.Length > 800 ? body.Substring(0, 800) + "..." : body;
        }
    }
}
