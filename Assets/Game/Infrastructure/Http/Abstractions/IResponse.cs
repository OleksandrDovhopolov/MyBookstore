namespace Game.Http {
    public interface IResponse {
        bool IsSuccess { get; }
        byte[] Data { get; }
        string DataAsText { get; }

        int StatusCode { get; }
        string ErrorMessage { get; }

        string GetFirstHeaderValue(string name);
    }
}
