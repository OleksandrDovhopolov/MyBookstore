using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Server.Commands;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Тонкий клиент admin API (`/api/admin/configs/*`).
    /// Прямо на UnityWebRequest — нужен PUT и кастомные заголовки (Basic auth, If-Match),
    /// чего нет в Game.Http (только GET/POST). Editor-only.
    /// </summary>
    internal sealed class AdminApiClient
    {
        private const string AdminBase = "/api/admin/configs/";
        private const int TimeoutSeconds = 30;

        public sealed class Result
        {
            public bool Success;
            public long StatusCode;
            public string Body;         // raw response body
            public string ETag;         // canonical (no quotes)
            public string Error;        // human-readable
        }

        public string SectionUrl(string section, string environment)
            => $"{Trim(BaseUrl)}{AdminBase}{Uri.EscapeDataString(section)}?environment={Uri.EscapeDataString(environment)}";

        public string HistoryUrl(string section, string environment)
            => $"{Trim(BaseUrl)}{AdminBase}{Uri.EscapeDataString(section)}/history?environment={Uri.EscapeDataString(environment)}";

        public string RollbackUrl(string section, string environment, long toVersion)
            => $"{Trim(BaseUrl)}{AdminBase}{Uri.EscapeDataString(section)}/rollback?environment={Uri.EscapeDataString(environment)}&to={toVersion}";

        public string PromoteUrl(string section, string from, string to)
            => $"{Trim(BaseUrl)}{AdminBase}{Uri.EscapeDataString(section)}/promote?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}";

        public UniTask<Result> GetAsync(string url, CancellationToken ct)
            => SendAsync(url, UnityWebRequest.kHttpVerbGET, null, null, ct);

        /// <summary>
        /// PUT с обязательным If-Match. Передавай <paramref name="ifMatch"/>="bootstrap"
        /// для первой публикации секции (§8.2 spec).
        /// </summary>
        public UniTask<Result> PutAsync(string url, string body, string ifMatch, CancellationToken ct)
            => SendAsync(url, UnityWebRequest.kHttpVerbPUT, body, ifMatch, ct);

        public UniTask<Result> PostAsync(string url, CancellationToken ct)
            => SendAsync(url, UnityWebRequest.kHttpVerbPOST, null, null, ct);

        private static string BaseUrl => ConfigEditorSettings.BaseUrl;

        private static string Trim(string s) => string.IsNullOrEmpty(s) ? s : s.TrimEnd('/');

        private static async UniTask<Result> SendAsync(
            string url, string verb, string body, string ifMatch, CancellationToken ct)
        {
            // Логируем URL (НЕ заголовки/тело — там пароль и потенциально секретные данные).
            Debug.Log($"[AdminApiClient] {verb} {url}");

            using var req = new UnityWebRequest(url, verb);
            req.timeout = TimeoutSeconds;
            req.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.SetRequestHeader("Content-Type", "application/json");
            }

            var basic = BuildBasicAuth();
            if (basic != null)
                req.SetRequestHeader("Authorization", basic);

            if (!string.IsNullOrEmpty(ifMatch))
            {
                // На провод — HTTP-корректный кавыченный entity-tag.
                var canonical = GetConfigCommand.NormalizeEtag(ifMatch);
                req.SetRequestHeader("If-Match", "\"" + canonical + "\"");
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
                throw;
            }

            var status = req.responseCode;
            var success = status >= 200 && status < 300;
            var resp = new Result
            {
                Success = success,
                StatusCode = status,
                Body = req.downloadHandler != null ? req.downloadHandler.text : null,
                ETag = GetConfigCommand.NormalizeEtag(req.GetResponseHeader("ETag")),
                Error = success ? null : ExtractError(req)
            };

            Debug.Log($"[AdminApiClient] {verb} {url} → {status} ({(success ? "ok" : "error")})");
            return resp;
        }

        private static string BuildBasicAuth()
        {
            var user = ConfigEditorSettings.Username;
            var pass = ConfigEditorSettings.Password;
            if (string.IsNullOrEmpty(user) && string.IsNullOrEmpty(pass))
                return null;
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + pass));
            return "Basic " + token;
        }

        private static string ExtractError(UnityWebRequest req)
        {
            var body = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (!string.IsNullOrWhiteSpace(body))
                return body.Length > 800 ? body.Substring(0, 800) + "..." : body;
            return string.IsNullOrEmpty(req.error) ? $"HTTP {req.responseCode}" : req.error;
        }
    }
}
