namespace Game.Configs.Server
{
    /// <summary>
    /// Тот же .NET сервер, что и для сейвов (см. Save/Config/SaveBackendConfig).
    /// Public-методы конфигов реализованы и активны. Environment не передаётся —
    /// сервер по умолчанию отдаёт prod. (dev при необходимости: дописать "?environment=dev"
    /// к путям ниже; не используется в текущем цикле.)
    /// </summary>
    public sealed class ConfigsBackendConfig : IConfigsBackendConfig
    {
        public string BaseUrl => "https://gameserver-production-be8b.up.railway.app/api/v1/";
        public string ManifestPath => "configs/manifest";
        public string ConfigPathFormat => "configs/{0}";
        public int RequestTimeoutMs => 5000;
    }
}
