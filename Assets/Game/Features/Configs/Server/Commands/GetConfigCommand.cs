using System;
using Game.Commands;
using Game.Http;

namespace Game.Configs.Server.Commands
{
    /// <summary>
    /// GET /configs/{name} с условным заголовком If-None-Match (ETag).
    /// 200 → Json + ETag; 304 → NotModified (используем локальный снапшот).
    /// </summary>
    public sealed class GetConfigCommand : AbstractServiceCommand
    {
        private readonly string _ifNoneMatch;

        public string Json { get; private set; }
        public string ETag { get; private set; }
        public bool NotModified { get; private set; }

        public GetConfigCommand(
            IConnectionService connectionService,
            ICommandLogger logger,
            ICommandErrorReporter errorReporter,
            string url,
            string ifNoneMatch = null)
            : base(connectionService, logger, errorReporter)
        {
            endpoint = url;
            method = HTTPMethods.Get;
            _ifNoneMatch = ifNoneMatch;
        }

        protected override void ExecInternal()
        {
            Json = null;
            ETag = null;
            NotModified = false;
            base.ExecInternal();
        }

        protected override void FillData()
        {
            base.FillData();
            // На провод — HTTP-корректный кавыченный entity-tag, даже если хранится канонический (без кавычек).
            var canonical = NormalizeEtag(_ifNoneMatch);
            if (!string.IsNullOrEmpty(canonical))
                request.SetHeader("If-None-Match", "\"" + canonical + "\"");
        }

        protected override void ProcessSuccessResponse(IResponse resp)
        {
            Json = resp.DataAsText;
            ETag = NormalizeEtag(resp.GetFirstHeaderValue("ETag"));
            Error = BaseCommandsErrors.NoError;
        }

        // 304 Not Modified — не ошибка: контент не менялся, оставляем снапшот.
        protected override void OnServerSendError(IResponse resp)
        {
            if (resp.StatusCode == 304)
            {
                NotModified = true;
                ETag = NormalizeEtag(resp.GetFirstHeaderValue("ETag"));
                Error = BaseCommandsErrors.NoError;
                NotifyComplete();
                return;
            }

            base.OnServerSendError(resp);
        }

        /// <summary>
        /// Канонизирует ETag для сравнения: срезает ведущий weak-префикс "W/" и обрамляющие кавычки.
        /// Нужно потому, что сервер отдаёт etag в заголовке в кавычках, а в манифесте — без.
        /// </summary>
        public static string NormalizeEtag(string etag)
        {
            if (string.IsNullOrEmpty(etag))
                return etag;

            var s = etag.Trim();
            if (s.StartsWith("W/", StringComparison.Ordinal))
                s = s.Substring(2).Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2);

            return s;
        }
    }
}
