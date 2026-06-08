namespace Game.Configs.Server
{
    /// <summary>
    /// Адрес и пути публичных конфиг-методов на .NET сервере.
    /// Методы выкатывает backend по спеку из docs/CONFIG_CACHE_SYSTEM.md (Phase 4).
    /// </summary>
    public interface IConfigsBackendConfig
    {
        string BaseUrl { get; }

        /// <summary>Путь манифеста: GET → [{ name, version, etag }].</summary>
        string ManifestPath { get; }

        /// <summary>Шаблон пути конфига, {0} = имя файла. GET с If-None-Match → JSON / 304.</summary>
        string ConfigPathFormat { get; }

        int RequestTimeoutMs { get; }
    }
}
