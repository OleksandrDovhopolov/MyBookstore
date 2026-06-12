using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Configs.Remote
{
    /// <summary>
    /// Override layer on top of base configs via Firebase RC.
    /// Key convention: the RC key "cfg_&lt;fileName&gt;" holds a JSON object
    /// { "&lt;id&gt;": { ...partial... }, ... }. The partial is merged over the base
    /// config object in ConfigsService — this is the A/B and targeting hook.
    /// Underscore (not dot) because Firebase RC keys allow only letters, digits and '_'.
    /// </summary>
    public sealed class RemoteConfigOverrideSource : IConfigOverrideSource
    {
        private const string KeyPrefix = "cfg_";

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
                {
                    Debug.Log($"[RemoteConfigOverrideSource] '{KeyPrefix}{fileName}' present, but no entry for id='{id}'.");
                    return false;
                }

                partialJson = token.ToString();
                Debug.Log($"[RemoteConfigOverrideSource] override {fileName}/{id} -> {partialJson}");
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
