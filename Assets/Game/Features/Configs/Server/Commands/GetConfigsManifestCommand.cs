using Game.Commands;
using Game.Http;

namespace Game.Configs.Server.Commands
{
    /// <summary>
    /// GET /configs/manifest → JSON-массив [{ name, version, etag }].
    /// Сырой ответ отдаётся в ManifestJson, парсинг — в ServerConfigSource.
    /// </summary>
    public sealed class GetConfigsManifestCommand : AbstractServiceCommand
    {
        public string ManifestJson { get; private set; }

        public GetConfigsManifestCommand(
            IConnectionService connectionService,
            ICommandLogger logger,
            ICommandErrorReporter errorReporter,
            string url)
            : base(connectionService, logger, errorReporter)
        {
            endpoint = url;
            method = HTTPMethods.Get;
        }

        protected override void ExecInternal()
        {
            ManifestJson = null;
            base.ExecInternal();
        }

        protected override void ProcessSuccessResponse(IResponse resp)
        {
            ManifestJson = resp.DataAsText;
            Error = BaseCommandsErrors.NoError;
        }
    }
}
