using System.Text;
using Game.Commands;
using Game.Http;
using Newtonsoft.Json;

namespace Save.Storage.Commands
{
    // POST /save/global with body { "playerId": "...", "data": "<escaped json string>" }.
    // Server contract requires `data` as a JSON-escaped string, not an embedded object —
    // that's why FillData() is overridden to send raw JSON instead of form-data
    // (the default AbstractServiceCommand path uses WWWForm).
    public sealed class PostSaveGlobalCommand : AbstractServiceCommand
    {
        private readonly string _playerId;
        private readonly string _saveDataJson;

        public PostSaveGlobalCommand(
            IConnectionService connectionService,
            ICommandLogger logger,
            ICommandErrorReporter errorReporter,
            string url,
            string playerId,
            string saveDataJson)
            : base(connectionService, logger, errorReporter)
        {
            endpoint = url;
            method = HTTPMethods.Post;
            _playerId = playerId;
            _saveDataJson = saveDataJson;
        }

        protected override void FillData()
        {
            // Build {"playerId":"...","data":"<escaped json>"} — `data` becomes a JSON string
            // because _saveDataJson is passed as a string property, which JsonConvert escapes.
            var body = JsonConvert.SerializeObject(new { playerId = _playerId, data = _saveDataJson });
            request.SetHeader("Content-Type", "application/json");
            request.SetRawData(Encoding.UTF8.GetBytes(body));
        }

        protected override void ProcessSuccessResponse(IResponse resp)
        {
            // No payload expected on success; IsSucceed alone is the signal.
        }
    }
}
