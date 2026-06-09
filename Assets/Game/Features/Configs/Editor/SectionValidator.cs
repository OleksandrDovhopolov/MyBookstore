using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.Configs.Editor
{
    /// <summary>Одна проблема валидации, привязка к конкретному id если применимо.</summary>
    internal readonly struct ValidationIssue
    {
        public readonly string ItemId;     // null = section-level
        public readonly string Message;
        public ValidationIssue(string itemId, string message) { ItemId = itemId; Message = message; }
    }

    /// <summary>
    /// Валидация перед Publish (§9 спеки).
    /// Общие: массив; каждый item — object; id непустой и уникальный.
    /// Books extras: title непустой, basePrice ≥ 0, rarityWeight ≥ 0.
    /// </summary>
    internal static class SectionValidator
    {
        public static List<ValidationIssue> Validate(string section, JArray working)
        {
            var issues = new List<ValidationIssue>();
            if (working == null)
            {
                issues.Add(new ValidationIssue(null, "Section is null (expected JSON array)."));
                return issues;
            }

            var seen = new HashSet<string>();
            for (var i = 0; i < working.Count; i++)
            {
                var token = working[i];
                if (token is not JObject obj)
                {
                    issues.Add(new ValidationIssue(null, $"Item #{i} is not an object."));
                    continue;
                }

                var idToken = obj["id"];
                var id = idToken?.Type == JTokenType.String ? idToken.Value<string>() : null;

                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(new ValidationIssue(null, $"Item #{i}: 'id' is missing or empty."));
                    continue;
                }

                if (!seen.Add(id))
                {
                    issues.Add(new ValidationIssue(id, $"Duplicate id '{id}'."));
                    continue;
                }

                if (section == "books")
                {
                    var title = obj["title"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(title))
                        issues.Add(new ValidationIssue(id, "'title' is empty."));

                    var basePrice = obj["basePrice"]?.Value<double?>() ?? 0;
                    if (basePrice < 0)
                        issues.Add(new ValidationIssue(id, "'basePrice' must be >= 0."));

                    var rarity = obj["rarityWeight"]?.Value<double?>() ?? 0;
                    if (rarity < 0)
                        issues.Add(new ValidationIssue(id, "'rarityWeight' must be >= 0."));
                }
            }

            return issues;
        }
    }
}
