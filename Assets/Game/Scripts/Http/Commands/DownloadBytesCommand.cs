using Game.Commands;

namespace Game.Http {
    /// <summary>
    /// Пример конкретной REST-команды: GET по url, результат — байты ответа в памяти (LoadedBytes).
    /// Шаблон для своих команд (GET/POST с парсингом JSON и т.п.).
    /// </summary>
    public class DownloadBytesCommand : AbstractServiceCommand, IDownloadProgressCommand {
        public byte[] LoadedBytes { get; private set; }

        public long DownloadedBytes => LoadedBytes?.LongLength ?? 0;
        public long TotalBytes { get; private set; }

        public DownloadBytesCommand(IConnectionService connectionService, ICommandLogger logger, ICommandErrorReporter errorReporter, string url)
            : base(connectionService, logger, errorReporter) {
            endpoint = url;
            method = HTTPMethods.Get;
        }

        protected override void ExecInternal() {
            LoadedBytes = null;
            TotalBytes = 0;
            base.ExecInternal();
        }

        protected override void ProcessSuccessResponse(IResponse resp) {
            if (resp.Data != null) {
                LoadedBytes = resp.Data;
                TotalBytes = resp.Data.LongLength;
                Error = BaseCommandsErrors.NoError;
                LogInfo($"DownloadedSize = {resp.Data.Length}");
            } else {
                Terminate(ConnectionCommandsErrors.WrongResponseError, "Downloaded bytes is null");
            }
        }
    }
}
