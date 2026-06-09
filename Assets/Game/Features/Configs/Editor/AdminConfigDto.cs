using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Ответ admin GET и PUT (§3.1 / §3.2 спеки сервера):
    /// { name, environment, version, etag, json: [...] }
    /// </summary>
    internal sealed class AdminConfigDto
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("environment")] public string Environment;
        [JsonProperty("version")] public long Version;
        [JsonProperty("etag")] public string Etag;
        [JsonProperty("json")] public JArray Json;
    }

    /// <summary>Один элемент истории (§3.3).</summary>
    internal sealed class HistoryEntryDto
    {
        [JsonProperty("version")] public long Version;
        [JsonProperty("etag")] public string Etag;
        [JsonProperty("updatedBy")] public string UpdatedBy;
        [JsonProperty("updatedAt")] public DateTime UpdatedAt;
        [JsonProperty("comment")] public string Comment;
    }
}
