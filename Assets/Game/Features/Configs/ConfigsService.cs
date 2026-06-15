using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Configs
{
    /// <summary>
    /// Резолвит конфиги за единым контрактом: ConfigCache → (miss) base source → RC override merge.
    /// Тип десериализуется лениво при первом обращении и кладётся в кэш целиком.
    /// </summary>
    public sealed class ConfigsService : IConfigsService
    {
        private const string LogPrefix = "[ConfigsService]";

        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault();
        private static readonly JsonMergeSettings MergeSettings = new()
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        };

        private readonly IConfigSource _source;
        private readonly IConfigOverrideSource _overrides;
        private readonly ConfigCache<IConfig> _cache = new();

        private bool _warmedUp;

        public ConfigsService(IConfigSource source, IConfigOverrideSource overrides)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _overrides = overrides ?? new NullConfigOverrideSource();
        }

        public async UniTask WarmupAsync(CancellationToken ct)
        {
            if (_warmedUp) return;
            
            Debug.LogWarning($"{LogPrefix} WarmupAsync");
            await _source.WarmupAsync(ct);
            _warmedUp = true;
        }

        public T Get<T>(string id) where T : class, IConfig
        {
            EnsureTypeLoaded<T>();
            if (_cache.TryGet<T>(id, out var config))
                return config;

            Debug.LogWarning($"{LogPrefix} Get<{typeof(T).Name}>('{id}') not found.");
            return null;
        }

        public bool TryGet<T>(string id, out T config) where T : class, IConfig
        {
            EnsureTypeLoaded<T>();
            return _cache.TryGet(id, out config);
        }

        public async UniTask<T> GetAsync<T>(string id) where T : class, IConfig
        {
            if (!_warmedUp)
                await WarmupAsync(CancellationToken.None);
            return Get<T>(id);
        }

        public bool IsExists<T>(string id) where T : class, IConfig
        {
            EnsureTypeLoaded<T>();
            return _cache.Contains<T>(id);
        }

        public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
        {
            EnsureTypeLoaded<T>();
            var map = _cache.GetTypeMap(typeof(T));
            if (map == null) return Array.Empty<T>();

            var list = new List<T>(map.Count);
            foreach (var entry in map.Values)
            {
                if (entry is T typed)
                    list.Add(typed);
            }
            return list;
        }

        private void EnsureTypeLoaded<T>() where T : class, IConfig
        {
            if (_cache.HasType(typeof(T)))
                return;

            if (!_warmedUp)
                Debug.LogWarning($"{LogPrefix} Accessing {typeof(T).Name} before WarmupAsync — source may be empty.");

            _cache.SetType(typeof(T), DeserializeType<T>());
        }

        private Dictionary<string, IConfig> DeserializeType<T>() where T : class, IConfig
        {
            var map = new Dictionary<string, IConfig>();
            var fileName = ResolveFileName(typeof(T));

            var raw = _source.GetRaw(fileName);
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogWarning($"{LogPrefix} No config file '{fileName}' for {typeof(T).Name}.");
                return map;
            }

            JArray array;
            try
            {
                array = JArray.Parse(raw);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to parse '{fileName}' as JSON array: {ex.Message}");
                return map;
            }

            foreach (var token in array)
            {
                if (token is not JObject obj)
                    continue;

                var id = ReadId(obj);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"{LogPrefix} Entry without id in '{fileName}', skipped.");
                    continue;
                }

                if (_overrides.TryGetOverride(fileName, id, out var partial) && !string.IsNullOrWhiteSpace(partial))
                {
                    try
                    {
                        obj.Merge(JObject.Parse(partial), MergeSettings);
                        Debug.Log($"{LogPrefix} applied override for {fileName}/{id}.");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"{LogPrefix} Bad RC override for {fileName}/{id}: {ex.Message}");
                    }
                }

                try
                {
                    var config = obj.ToObject<T>(Serializer);
                    if (config != null)
                        map[config.Id] = config;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogPrefix} Failed to deserialize {typeof(T).Name} id='{id}': {ex.Message}");
                }
            }

            return map;
        }

        private static string ResolveFileName(Type type)
        {
            var attr = (ConfigFileAttribute)Attribute.GetCustomAttribute(type, typeof(ConfigFileAttribute));
            if (attr == null)
                throw new InvalidOperationException(
                    $"{type.Name} is missing [ConfigFile(\"...\")]. Add it to map the type to a config file.");
            return attr.FileName;
        }

        // id matching is case-insensitive so JSON may use "id" while the model exposes Id.
        private static string ReadId(JObject obj)
        {
            foreach (var prop in obj.Properties())
            {
                if (string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase))
                    return prop.Value?.ToString();
            }
            return null;
        }
    }
}
