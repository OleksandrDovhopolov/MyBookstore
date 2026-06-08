using UnityEngine.Networking;

namespace Game.Http {
    public class UnityWebResponse : IResponse {
        private readonly UnityWebRequest _request;

        public UnityWebResponse(UnityWebRequest request) {
            _request = request;
        }

        public bool IsSuccess => _request.responseCode >= 200 && _request.responseCode < 300;
        public byte[] Data => _request.downloadHandler?.data;
        public string DataAsText => _request.downloadHandler?.text;
        public int StatusCode => (int)_request.responseCode;
        public string ErrorMessage => _request.error;

        public string GetFirstHeaderValue(string name) => _request.GetResponseHeader(name);
    }
}
