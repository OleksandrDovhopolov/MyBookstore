namespace Game.Configs
{
    /// <summary>
    /// Override-слой поверх базовых конфигов. Реализуется через Firebase Remote Config
    /// (A/B, таргетинг, фиче-флаги): отдаёт partial-JSON, который мёржится поверх
    /// базового объекта конфига при десериализации. Семантика — как у _partial
    /// из прод-системы (см. docs/CONFIG_CACHE_SYSTEM.md).
    /// </summary>
    public interface IConfigOverrideSource
    {
        /// <summary>
        /// Возвращает true и partial-JSON, если для (файл, id) есть override.
        /// partial мёржится поверх базового JObject (только перечисленные поля).
        /// </summary>
        bool TryGetOverride(string fileName, string id, out string partialJson);
    }

    /// <summary>Дефолтная реализация без override (Phase 1, до подключения Firebase RC).</summary>
    public sealed class NullConfigOverrideSource : IConfigOverrideSource
    {
        public bool TryGetOverride(string fileName, string id, out string partialJson)
        {
            partialJson = null;
            return false;
        }
    }
}
