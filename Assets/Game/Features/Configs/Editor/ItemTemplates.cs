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
                    ["titleKey"] = "",
                    ["authorKey"] = "",
                    ["descriptionKey"] = "",
                    ["genres"] = new JArray(),
                    ["rarityWeight"] = 0.0,
                    ["published"] = 0,
                    ["pages"] = 0,
                    ["qualities"] = new JArray()
                },
                "locations" => new JObject
                {
                    ["id"] = "",
                    ["displayNameKey"] = "",
                    ["entryCost"] = 0,
                    ["locationAddress"] = "",
                    ["demandGenres"] = new JArray(),
                    ["customerTrafficPercentDelta"] = 0.0
                },
                "sample_requests" => new JObject
                {
                    ["id"] = "",
                    ["descriptionKey"] = "",
                    ["genre"] = "",
                    ["bookTitle"] = "",
                    ["enabled"] = true,
                    ["conditions"] = new JObject
                    {
                        ["all"] = new JArray()
                    }
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
