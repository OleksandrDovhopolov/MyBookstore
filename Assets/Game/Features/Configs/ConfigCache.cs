using System;
using System.Collections.Generic;

namespace Game.Configs
{
    /// <summary>
    /// Типобезопасный in-memory кэш конфигов. Хранит уже десериализованные объекты,
    /// сгруппированные по типу и проиндексированные по Id. Десериализация ленивая:
    /// тип попадает сюда при первом обращении к нему (см. ConfigsService).
    /// Без существование-словаря и LRU — для MVP это избыточно.
    /// </summary>
    public sealed class ConfigCache<TBase> where TBase : class
    {
        private readonly Dictionary<Type, Dictionary<string, TBase>> _byType = new();

        public bool HasType(Type type) => _byType.ContainsKey(type);

        public void SetType(Type type, Dictionary<string, TBase> entries) => _byType[type] = entries;

        public bool TryGet<T>(string id, out T value) where T : class, TBase
        {
            value = null;
            if (id == null) return false;
            if (!_byType.TryGetValue(typeof(T), out var map)) return false;
            if (!map.TryGetValue(id, out var raw)) return false;
            value = raw as T;
            return value != null;
        }

        public bool Contains<T>(string id) where T : class, TBase
            => id != null
               && _byType.TryGetValue(typeof(T), out var map)
               && map.ContainsKey(id);

        public IReadOnlyDictionary<string, TBase> GetTypeMap(Type type)
            => _byType.TryGetValue(type, out var map) ? map : null;
    }
}
