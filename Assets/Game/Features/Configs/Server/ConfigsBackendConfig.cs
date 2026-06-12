namespace Game.Configs.Server
{
    /// <summary>
    /// Тот же .NET сервер, что и для сейвов (см. Save/Config/SaveBackendConfig).
    /// Environment передаётся ЯВНО через query-параметр — сервер при отсутствии параметра
    /// возвращает пустой/dev-список, а не prod (вопреки тому, что предполагалось раньше).
    /// MVP: захардкожен prod. Конфигурируемый environment (dev/staging/prod через define
    /// или SO-настройку) — отдельная задача в backlog.
    /// </summary>
    public sealed class ConfigsBackendConfig : IConfigsBackendConfig
    {
        private const string Environment = "prod";

        public string BaseUrl => "https://gameserver-production-be8b.up.railway.app/api/v1/";
        public string ManifestPath => $"configs/manifest?environment={Environment}";
        public string ConfigPathFormat => $"configs/{{0}}?environment={Environment}";
        public int RequestTimeoutMs => 5000;
    }
}
