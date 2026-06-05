using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Game.Commands;

namespace Game.Http {
    /// <summary>
    /// База REST/HTTP-команд. Инкапсулирует отправку запроса через IConnectionService,
    /// разбор ответа, коды статусов, таймауты, проверку интернета и репорт ошибок.
    /// От конкретного HTTP-бэкенда не зависит — работает через IRequest/IResponse.
    /// </summary>
    public abstract class AbstractServiceCommand : AbstractProgressCommand {
        protected internal string endpoint;
        protected internal HTTPMethods method = HTTPMethods.Post;

        protected IRequest request;
        protected internal int execCount;
        private float _requestStartTime;
        protected float requestTime;

        protected int respStatusCode;
        protected string respErrorMessage;

        protected ConnectionCheckBehaviour? _connectionCheckBehaviour;
        public ConnectionCheckBehaviour ConnectionCheckBehaviour {
            get => _connectionCheckBehaviour ?? _connectionService.CheckInternetBehaviour;
            set => _connectionCheckBehaviour = value;
        }

        protected virtual bool NeedToCheckConnectionBeforeExecution => true;

        private readonly IConnectionService _connectionService;

        protected AbstractServiceCommand(IConnectionService connectionService, ICommandLogger logger, ICommandErrorReporter errorReporter)
            : base(logger, errorReporter) {
            Error = BaseCommandsErrors.UnknownError;
            _connectionService = connectionService;
        }

        protected override void ExecInternal() {
            Error = BaseCommandsErrors.UnknownError;
            respStatusCode = 0;
            respErrorMessage = string.Empty;

            if (string.IsNullOrEmpty(endpoint)) {
                Terminate(ConnectionCommandsErrors.NullEndpointError, "endpoint is null or empty");
                return;
            }

            execCount++;

            if (!NeedToCheckConnectionBeforeExecution || _connectionService.IsConnected) {
                ExecRequest();
            } else {
                Error = BaseCommandsErrors.NoInternetError;
                ProcessConnectionFailBehaviour();
            }
        }

        private void ProcessConnectionFailBehaviour() {
            switch (ConnectionCheckBehaviour) {
                case ConnectionCheckBehaviour.NoInternetSignalWithRetry:
                    ProcessNoInternetSignalWithRetry();
                    break;
                case ConnectionCheckBehaviour.NoInternetSignalOnce:
                    ProcessNoInternetSignalOnce();
                    break;
                case ConnectionCheckBehaviour.SilentWithRetry:
                    ProcessSilentWithRetry();
                    break;
                case ConnectionCheckBehaviour.SilentWithComplete:
                case ConnectionCheckBehaviour.ErrorLogsWithComplete:
                    NotifyComplete(BaseCommandsErrors.NoInternetError);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual void ProcessNoInternetSignalWithRetry() {
            _connectionService.HandleNoInternet(Retry);
        }

        protected virtual void ProcessNoInternetSignalOnce() {
            _connectionService.HandleNoInternet(RetryWithConnectionCheck);
        }

        protected virtual void ProcessSilentWithRetry() {
            _connectionService.SubscribeOnceOnInternetBecomeAvailable(Retry);
        }

        protected virtual void ExecRequest() {
            var requestParams = CreateRequestParams();
            request = _connectionService.CreateRequest(requestParams);
            FillData();
            _requestStartTime = Time.realtimeSinceStartup;
            request.Send();
        }

        protected virtual Dictionary<string, string> GetRequestTextData() {
            return null;
        }

        protected virtual Dictionary<string, byte[]> GetRequestBinaryData() {
            return null;
        }

        protected virtual void FillData() {
            var textData = GetRequestTextData();
            if (textData != null) {
                foreach (var (key, value) in textData) {
                    if (key == null || value == null) {
                        LogWarning($"Try send null data:\n{key} = {value}");
                        continue;
                    }
                    request.AddField(key, value);
                }
            }

            var binaryData = GetRequestBinaryData();
            if (binaryData != null) {
                foreach (var (key, bytes) in binaryData) {
                    if (key == null || bytes == null) {
                        LogWarning($"Try send null binary data:\n{key} = {bytes?.Length} bytes");
                        continue;
                    }
                    request.AddBinaryData(key, bytes);
                }
            }
        }

        protected internal virtual RequestParams CreateRequestParams() {
            return new RequestParams(new Uri(endpoint), method, ResultHandler) {
                DownloadProgress = OnDownloadProgress
            };
        }

        protected internal virtual void ResultHandler(IRequest req, IResponse resp) {
            try {
                requestTime = Time.realtimeSinceStartup - _requestStartTime;
                ParseResponse(req, resp);
            } catch (Exception e) {
                Error = BaseCommandsErrors.InternalCmdExceptionError;
                LogException(e);
                NotifyComplete(BaseCommandsErrors.InternalCmdExceptionError);
            }
        }

        private void ParseResponse(IRequest req, IResponse resp) {
            switch (req.State) {
                case RequestStates.Finished:
                    if (resp.IsSuccess) {
                        Error = BaseCommandsErrors.NoError;
                        ProcessSuccessResponse(resp);
                        NotifyComplete();
                    } else {
                        respStatusCode = resp.StatusCode;
                        respErrorMessage = resp.ErrorMessage;
                        OnServerSendError(resp);
                    }
                    break;
                case RequestStates.Error:
                    OnHttpError(req.Exception);
                    break;
                case RequestStates.Aborted:
                    OnRequestAborted();
                    break;
                case RequestStates.ConnectionTimedOut:
                    OnConnectionTimeOut();
                    break;
                case RequestStates.TimedOut:
                    OnRequestTimeOut();
                    break;
                default:
                    ProcessError(BaseCommandsErrors.UnknownError, $"ResultHandler receive unsupported request state = {req.State}");
                    NotifyComplete();
                    break;
            }
        }

        protected internal void OnDownloadProgress(int downloaded, int length) {
            if (length != 0) {
                OnProgress(100 * downloaded / length);
            }
        }

        protected string GetQueryString(Dictionary<string, string> data) {
            var query = new List<string>(data.Count);
            query.AddRange(data.Select(p => $"{UnityWebRequest.EscapeURL(p.Key)}={UnityWebRequest.EscapeURL(p.Value)}"));

            return "?" + string.Join("&", query.ToArray());
        }

        protected virtual void ProcessSuccessResponse(IResponse resp) {
            ProcessSuccess(resp.DataAsText);
        }

        protected virtual void ProcessSuccess(string text) {
            LogDebug($" processSuccess: {text}");
        }

        protected virtual void OnServerSendError(IResponse resp) {
            var errorMessage =
                "Request finished Successfully, but the server sent an error. " +
                $"Status Code: {resp.StatusCode}-{resp.ErrorMessage} " +
                $"Message: {resp.DataAsText}";

            switch (resp.StatusCode) {
                case 404:
                    ProcessError(ConnectionCommandsErrors.NotFoundError, errorMessage);
                    break;
                case 403:
                    ProcessError(ConnectionCommandsErrors.Forbidden, errorMessage);
                    break;
                case >= 300 and <= 499:
                    ProcessError(ConnectionCommandsErrors.RequestError, errorMessage);
                    break;
                case 503:
                    ProcessError(ConnectionCommandsErrors.ServiceUnavailable, errorMessage);
                    break;
                default:
                    ProcessError(ConnectionCommandsErrors.ServerError, errorMessage);
                    break;
            }
            NotifyComplete();
        }

        protected virtual void OnHttpError(Exception ex) {
            ProcessError(ConnectionCommandsErrors.HttpError, "Request Finished with Error! " +
                (ex != null ? (ex.Message + "\n" + ex.StackTrace) : "No Exception"));
            NotifyComplete();
        }

        protected virtual void OnRequestAborted() {
            ProcessError(ConnectionCommandsErrors.RequestAbortedError);
            NotifyComplete();
        }

        protected virtual void OnConnectionTimeOut() {
            ProcessError(ConnectionCommandsErrors.ConnectionTimeoutError);
            NotifyComplete();
        }

        protected virtual void OnRequestTimeOut() {
            ProcessError(ConnectionCommandsErrors.RequestTimeoutError);
            NotifyComplete();
        }

        private void ProcessError(CommandError error, string message = null) {
            Error = new CommandError(error, message);
            ProcessError();
        }

        protected virtual void ProcessError() {
            LogCommandErrorInformation();
        }

        protected void LogCommandErrorInformation() {
            if (Error == ConnectionCommandsErrors.RequestAbortedError) {
                LogWarning(Error.ToString());
            } else {
                LogError(Error.ToString());
            }
        }

        protected override void PostExecuteActions() {
            base.PostExecuteActions();

            request = null;

            _connectionService.UnsubscribeFromInternetBecomeAvailable(Retry);

            if (IsSucceed || !IsNeedNotifyToBugTrackerToComplete()) {
                return;
            }

            var errorMessage = $"Error: {Error} url: {endpoint} statusCode: {respStatusCode} msg: {respErrorMessage}";
            ReportException(Error.Exception, errorMessage);
        }

        protected internal virtual bool IsNeedNotifyToBugTrackerToComplete() {
            if (Error == BaseCommandsErrors.NoError ||
                Error == ConnectionCommandsErrors.HttpError ||
                Error == ConnectionCommandsErrors.RequestError ||
                Error == ConnectionCommandsErrors.RequestTimeoutError ||
                Error == ConnectionCommandsErrors.ConnectionTimeoutError ||
                Error == BaseCommandsErrors.NoInternetError) {
                return false;
            }
            return true;
        }

        protected override string GetLogName() {
            return base.GetLogName() + " Url = " + endpoint;
        }

        protected void RetryWithConnectionCheck() {
            if (_connectionService.IsConnected) {
                Retry();
            } else {
                TerminateInternal();
            }
        }

        protected void Terminate(CommandError error, string errorMessage) {
            Error = new CommandError(error, errorMessage);
            request?.Abort();
            Terminate();
        }

        protected void TerminateInternal() {
            request?.Abort();
            Terminate();
        }
    }
}
