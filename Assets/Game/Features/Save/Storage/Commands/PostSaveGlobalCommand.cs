using System.Text;
using Game.Commands;
using Game.Http;
using Newtonsoft.Json;

namespace Save.Storage.Commands
{
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

        public static string BuildRequestBody(string playerId, string saveDataJson)
        {
            return JsonConvert.SerializeObject(new { playerId, data = saveDataJson });
        }

        protected override void FillData()
        {
            var body = BuildRequestBody(_playerId, _saveDataJson);
            request.SetHeader("Content-Type", "application/json");
            request.SetRawData(Encoding.UTF8.GetBytes(body));
        }

        protected override void ProcessSuccessResponse(IResponse resp)
        {
        }
    }
}
