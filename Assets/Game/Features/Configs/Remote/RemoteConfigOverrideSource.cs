using System;
using System.Collections.Generic;
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
    ///
    /// The RC key is fetched and parsed ONCE per file (see <see cref="IConfigOverrideSource"/> on why the
    /// contract is table-shaped). No caching across calls: ConfigsService parses each file once, so a
    /// cache would only add staleness risk if RC re-activates.
    /// </summary>
    public sealed class RemoteConfigOverrideSource : IConfigOverrideSource
    {
        private const string LogPrefix = "[RemoteConfigOverrideSource]";
        private const string KeyPrefix = "cfg_";

        private readonly IRemoteConfigService _rc;

        public RemoteConfigOverrideSource(IRemoteConfigService rc)
        {
            _rc = rc;
        }

        public bool TryGetOverrides(string fileName, out IReadOnlyDictionary<string, string> partialsById)
        {
            partialsById = null;
            if (_rc == null) return false;

            var key = KeyPrefix + fileName;
            if (!_rc.TryGetString(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return false;

            try
            {
                var table = JObject.Parse(raw);
                var map = new Dictionary<string, string>(table.Count, StringComparer.Ordinal);

                foreach (var entry in table)
                {
                    if (entry.Value == null) continue;
                    map[entry.Key] = entry.Value.ToString();
                }

                if (map.Count == 0) return false;

                // One line per file instead of one per config id. Lists the ids so a table that targets
                // something absent from the catalog (the classic content bug) is visible at a glance.
                Debug.Log($"{LogPrefix} '{key}': {map.Count} override entry(ies) for [{string.Join(", ", map.Keys)}].");

                partialsById = map;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} bad RC value for '{key}': {ex.Message}");
                return false;
            }
        }
    }
}
