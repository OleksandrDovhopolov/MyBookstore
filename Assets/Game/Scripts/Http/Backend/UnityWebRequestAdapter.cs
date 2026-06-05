using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Http {
    /// <summary>
    /// Реализация IRequest на встроенном UnityWebRequest (без платных зависимостей).
    /// Поля/заголовки/тело буферизуются и применяются в момент Send().
    /// </summary>
    public class UnityWebRequestAdapter : IRequest {
        private readonly IRequestParams _params;
        private readonly Action<IRequest, IResponse> _requestFinished;
        private readonly Action<int, int> _downloadProgress;

        private readonly Dictionary<string, string> _headers = new();
        private WWWForm _form;
        private byte[] _rawData;

        private UnityWebRequest _request;
        private UniTaskCompletionSource _promise;

        private RequestStates _state = RequestStates.Processing;
        private bool _aborted;

        public Exception Exception { get; private set; }
        public TimeSpan RequestTimeout { get; set; }

        public RequestStates State => _state;

        public UnityWebRequestAdapter(IRequestParams p) {
            _params = p;
            _requestFinished = p.RequestFinished;
            _downloadProgress = p.DownloadProgress;
            if (p.Timeout > 0) {
                RequestTimeout = TimeSpan.FromMilliseconds(p.Timeout);
            }
        }

        public void AddHeader(string name, string value) => _headers[name] = value;
        public void SetHeader(string name, string value) => _headers[name] = value;

        public void AddField(string name, string value) {
            _form ??= new WWWForm();
            _form.AddField(name, value);
        }

        public void AddBinaryData(string name, byte[] value) {
            _form ??= new WWWForm();
            _form.AddBinaryData(name, value);
        }

        public void SetRawData(byte[] data) {
            _rawData = data;
        }

        public void Send() {
            SendAsync().Forget();
        }

        public async UniTask SendAsync() {
            _promise ??= new UniTaskCompletionSource();

            try {
                _request = BuildRequest();
                ApplyHeaders();
                if (RequestTimeout > TimeSpan.Zero) {
                    _request.timeout = Mathf.Max(1, (int)RequestTimeout.TotalSeconds);
                }

                var op = _request.SendWebRequest();
                while (!op.isDone) {
                    ReportProgress();
                    await UniTask.Yield();
                }
                ReportProgress();

                _state = MapState(_request);
                Exception = MapException(_request);
            } catch (Exception e) {
                Exception = e;
                _state = RequestStates.Error;
            }

            try {
                var response = _request != null ? new UnityWebResponse(_request) : null;
                _requestFinished?.Invoke(this, response);
            } finally {
                _promise.TrySetResult();
            }
        }

        public void Abort() {
            _aborted = true;
            _request?.Abort();
        }

        private UnityWebRequest BuildRequest() {
            var url = _params.UriPath.ToString();

            if (_params.MethodType == HTTPMethods.Get) {
                return UnityWebRequest.Get(url);
            }

            // POST
            if (_rawData != null) {
                var uwr = new UnityWebRequest(url, "POST") {
                    uploadHandler = new UploadHandlerRaw(_rawData),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                return uwr;
            }

            if (_form != null) {
                return UnityWebRequest.Post(url, _form);
            }

            return new UnityWebRequest(url, "POST") {
                downloadHandler = new DownloadHandlerBuffer()
            };
        }

        private void ApplyHeaders() {
            foreach (var (name, value) in _headers) {
                _request.SetRequestHeader(name, value);
            }
        }

        private void ReportProgress() {
            if (_downloadProgress == null || _request == null) {
                return;
            }
            var percent = Mathf.RoundToInt(_request.downloadProgress * 100);
            _downloadProgress.Invoke(percent, 100);
        }

        private RequestStates MapState(UnityWebRequest uwr) {
            if (_aborted) {
                return RequestStates.Aborted;
            }

            switch (uwr.result) {
                case UnityWebRequest.Result.Success:
                case UnityWebRequest.Result.ProtocolError:
                    // ProtocolError = пришёл HTTP-ответ с кодом 4xx/5xx.
                    // Считаем запрос завершённым, успех/ошибку определит IResponse.IsSuccess по статус-коду.
                    return RequestStates.Finished;
                case UnityWebRequest.Result.ConnectionError:
                    return IsTimeout(uwr) ? RequestStates.TimedOut : RequestStates.Error;
                case UnityWebRequest.Result.DataProcessingError:
                    return RequestStates.Error;
                default:
                    return RequestStates.Error;
            }
        }

        private static bool IsTimeout(UnityWebRequest uwr) {
            return !string.IsNullOrEmpty(uwr.error) &&
                   uwr.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Exception MapException(UnityWebRequest uwr) {
            if (uwr.result == UnityWebRequest.Result.ConnectionError ||
                uwr.result == UnityWebRequest.Result.DataProcessingError) {
                return new Exception(uwr.error);
            }
            return null;
        }
    }
}
