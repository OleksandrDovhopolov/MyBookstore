using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Configs.Remote
{
    /// <summary>
    /// Override-слой поверх базовых конфигов через Firebase RC.
    /// Конвенция ключей: RC-ключ "cfg.&lt;fileName&gt;" хранит JSON-объект
    /// { "&lt;id&gt;": { ...partial... }, ... }. Partial мёржится поверх базового
    /// объекта конфига в ConfigsService — это и есть точка A/B и таргетинга.
    /// </summary>
    public sealed class RemoteConfigOverrideSource : IConfigOverrideSource
    {
        private const string KeyPrefix = "cfg.";

        private readonly IRemoteConfigService _rc;

        public RemoteConfigOverrideSource(IRemoteConfigService rc)
        {
            _rc = rc;
        }

        public bool TryGetOverride(string fileName, string id, out string partialJson)
        {
            partialJson = null;
            if (_rc == null)
                return false;

            if (!_rc.TryGetString(KeyPrefix + fileName, out var raw) || string.IsNullOrWhiteSpace(raw))
                return false;

            try
            {
                var token = JObject.Parse(raw)[id];
                if (token == null)
                    return false;

                partialJson = token.ToString();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RemoteConfigOverrideSource] bad RC value for '{KeyPrefix}{fileName}': {ex.Message}");
                return false;
            }
        }
    }
}
