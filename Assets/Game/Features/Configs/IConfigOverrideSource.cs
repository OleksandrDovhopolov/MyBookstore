using System.Collections.Generic;

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
        /// Возвращает ВСЮ override-таблицу файла разом: id → partial-JSON. Каждый partial мёржится поверх
        /// базового JObject (только перечисленные поля).
        ///
        /// Табличный, а не поштучный (fileName, id) контракт — намеренно: override-таблица разрежена
        /// (обычно 0-1 запись на файл), а конфигов в файле десятки. Поштучный вызов заставлял источник
        /// перезапрашивать и заново парсить весь RC-ключ на КАЖДЫЙ конфиг — 63 фетча + 63 парса на
        /// books.json ради одной записи. Здесь материализация одна на файл, дальше — O(1) лукап.
        /// </summary>
        bool TryGetOverrides(string fileName, out IReadOnlyDictionary<string, string> partialsById);
    }

    /// <summary>Дефолтная реализация без override (Phase 1, до подключения Firebase RC).</summary>
    public sealed class NullConfigOverrideSource : IConfigOverrideSource
    {
        public bool TryGetOverrides(string fileName, out IReadOnlyDictionary<string, string> partialsById)
        {
            partialsById = null;
            return false;
        }
    }
}
