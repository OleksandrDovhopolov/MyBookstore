using System;

namespace Game.Http {
    public interface IRequestParams {
        Uri UriPath { get; }
        HTTPMethods MethodType { get; }
        Action<IRequest, IResponse> RequestFinished { get; }
        long Timeout { get; }

        Action<int, int> DownloadProgress { get; }
    }
}
