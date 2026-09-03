using Game.Commands;
using Game.Http;

namespace Save.Storage.Commands
{
    // GET /save/global?playerId=<id>. Production returns {"data":{...},"lastModified":...}.
    // Legacy raw save JSON and {"data":"..."} responses are normalized through SaveGlobalPayloadParser.
    public sealed class GetSaveGlobalCommand : AbstractServiceCommand
    {
        public string NormalizedData { get; private set; }

        public GetSaveGlobalCommand(
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
            NormalizedData = null;
            base.ExecInternal();
        }

        protected override void ProcessSuccessResponse(IResponse resp)
        {
            var raw = resp.DataAsText;
            NormalizedData = SaveGlobalPayloadParser.ExtractDataForStorage(raw, out _);
        }
    }
}
