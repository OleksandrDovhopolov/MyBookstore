using System;

namespace Game.Http {
    public class RequestParams : IRequestParams {
        public Uri UriPath { get; }
        public HTTPMethods MethodType { get; }
        public Action<IRequest, IResponse> RequestFinished { get; }
        public long Timeout { get; private set; }

        public Action<int, int> DownloadProgress { get; set; }

        public RequestParams(Uri uri, HTTPMethods methodType, Action<IRequest, IResponse> callback = null, long timeout = 0) {
            UriPath = uri;
            MethodType = methodType;
            RequestFinished = callback;
            Timeout = timeout;
        }

        public RequestParams SetTimeout(long timeout) {
            Timeout = timeout;
            return this;
        }
    }
}
