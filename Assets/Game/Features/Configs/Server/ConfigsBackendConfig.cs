namespace Game.Configs.Server
{
    /// <summary>
    /// Тот же .NET сервер, что и для сейвов (см. Save/Config/SaveBackendConfig).
    /// Пути под конфиги пока не реализованы на бэке — заданы по спеку (Phase 4).
    /// </summary>
    public sealed class ConfigsBackendConfig : IConfigsBackendConfig
    {
        public string BaseUrl => "https://gameserver-production-be8b.up.railway.app/api/v1/";
        public string ManifestPath => "configs/manifest";
        public string ConfigPathFormat => "configs/{0}";
        public int RequestTimeoutMs => 5000;
    }
}
