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
            if (!string.IsNullOrEmpty(_ifNoneMatch))
                request.SetHeader("If-None-Match", _ifNoneMatch);
        }

        protected override void ProcessSuccessResponse(IResponse resp)
        {
            Json = resp.DataAsText;
            ETag = resp.GetFirstHeaderValue("ETag");
            Error = BaseCommandsErrors.NoError;
        }

        // 304 Not Modified — не ошибка: контент не менялся, оставляем снапшот.
        protected override void OnServerSendError(IResponse resp)
        {
            if (resp.StatusCode == 304)
            {
                NotModified = true;
                ETag = resp.GetFirstHeaderValue("ETag");
                Error = BaseCommandsErrors.NoError;
                NotifyComplete();
                return;
            }

            base.OnServerSendError(resp);
        }
    }
}
