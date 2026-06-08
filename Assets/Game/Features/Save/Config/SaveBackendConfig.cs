namespace Save.Config
{
    public sealed class SaveBackendConfig : ISaveBackendConfig
    {
        public string BaseUrl => "https://gameserver-production-be8b.up.railway.app/api/v1/";
        public string SavePath => "save/global";
        public int RequestTimeoutMs => 5000;
        public int RetryCount => 0;
    }
}
