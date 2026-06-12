using Newtonsoft.Json.Linq;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Шаблон нового item для секции (§8.4). Books — типизированный (как у POCO BookConfig);
    /// прочие — минимальный {"id":""}, дополнительные поля ГД добавит вручную.
    /// </summary>
    internal static class ItemTemplates
    {
        public static JObject Create(string section)
        {
            return section switch
            {
                "books" => new JObject
                {
                    ["id"] = "",
                    ["title"] = "",
                    ["author"] = "",
                    ["genre"] = "",
                    ["basePrice"] = 0,
                    ["rarityWeight"] = 0.0
                },
                "locations" => new JObject
                {
                    ["id"] = "",
                    ["displayName"] = "",
                    ["unlockCost"] = 0,
                    ["requiredLevel"] = 1
                },
                "requests" => new JObject
                {
                    ["id"] = "",
                    ["bookId"] = "",
                    ["rewardSoft"] = 0,
                    ["timeLimitSeconds"] = 0
                },
                "events" => new JObject
                {
                    ["id"] = "",
                    ["type"] = "",
                    ["startUtc"] = "",
                    ["endUtc"] = "",
                    ["rewardMultiplier"] = 1.0
                },
                _ => new JObject { ["id"] = "" }
            };
        }
    }
}
