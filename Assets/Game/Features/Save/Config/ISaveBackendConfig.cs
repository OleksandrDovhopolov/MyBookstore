namespace Save.Config
{
    public interface ISaveBackendConfig
    {
        string BaseUrl { get; }
        string SavePath { get; }
        int RequestTimeoutMs { get; }
        int RetryCount { get; }
    }
}
