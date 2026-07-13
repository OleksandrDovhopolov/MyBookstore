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
    /// Books extras: title непустой, genres[0] непустой, rarityWeight ≥ 0.
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

                    var genres = obj["genres"] as JArray;
                    var primaryGenre = genres != null && genres.Count > 0
                        ? genres[0]?.Value<string>()
                        : null;
                    if (string.IsNullOrWhiteSpace(primaryGenre))
                        issues.Add(new ValidationIssue(id, "'genres[0]' is missing or empty."));

                    var rarity = obj["rarityWeight"]?.Value<double?>() ?? 0;
                    if (rarity < 0)
                        issues.Add(new ValidationIssue(id, "'rarityWeight' must be >= 0."));
                }

                if (section == "hard_requests")
                {
                    ValidateHardRequest(id, obj, issues);
                }
            }

            return issues;
        }

        private static void ValidateHardRequest(string id, JObject obj, List<ValidationIssue> issues)
        {
            var description = obj["description"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(description))
                issues.Add(new ValidationIssue(id, "'description' is missing or empty."));

            if (obj["enabled"] == null)
                issues.Add(new ValidationIssue(id, "'enabled' is missing."));

            if (obj["conditions"] is not JObject conditions)
            {
                issues.Add(new ValidationIssue(id, "'conditions' is missing or not an object."));
                return;
            }

            ValidateConditionGroup(id, conditions["all"], "all", issues);
            ValidateConditionGroup(id, conditions["any"], "any", issues);
            ValidateConditionGroup(id, conditions["none"], "none", issues);
        }

        private static void ValidateConditionGroup(string id, JToken token, string groupName, List<ValidationIssue> issues)
        {
            if (token == null) return;
            if (token is not JArray array)
            {
                issues.Add(new ValidationIssue(id, $"'conditions.{groupName}' must be an array."));
                return;
            }

            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is not JObject condition)
                {
                    issues.Add(new ValidationIssue(id, $"'conditions.{groupName}[{i}]' must be an object."));
                    continue;
                }

                ValidateCondition(id, condition, $"conditions.{groupName}[{i}]", issues);
            }
        }

        private static void ValidateCondition(string id, JObject condition, string path, List<ValidationIssue> issues)
        {
            var type = condition["type"]?.Value<string>();
            var op = condition["operator"]?.Value<string>();
            var value = condition["value"];

            if (!IsKnownType(type))
                issues.Add(new ValidationIssue(id, $"'{path}.type' is unknown: '{type}'."));
            if (!IsKnownOperator(op))
                issues.Add(new ValidationIssue(id, $"'{path}.operator' is unknown: '{op}'."));
            if (value == null || value.Type == JTokenType.Null)
            {
                issues.Add(new ValidationIssue(id, $"'{path}.value' is missing."));
                return;
            }

            if (IsArrayOperator(op) && value is not JArray)
                issues.Add(new ValidationIssue(id, $"'{path}.value' must be an array for '{op}'."));
            if (op == "between" && (value is not JObject obj || obj["min"] == null || obj["max"] == null))
                issues.Add(new ValidationIssue(id, $"'{path}.value' must be {{ min, max }} for 'between'."));
            if (IsNumericType(type) && !IsNumericValue(op, value))
                issues.Add(new ValidationIssue(id, $"'{path}.value' must be numeric for '{type}'."));
            if (IsListType(type) && !IsArrayOperator(op) && value is JArray)
                issues.Add(new ValidationIssue(id, $"'{path}.value' must be scalar for '{op}'."));
        }

        private static bool IsKnownType(string type)
            => type == "genres" || type == "qualities" || type == "publicationYear" || type == "pages";

        private static bool IsKnownOperator(string op)
            => op == "equal" || op == "notEqual" ||
               op == "greater" || op == "greaterOrEqual" || op == "less" || op == "lessOrEqual" ||
               op == "between" ||
               op == "contains" || op == "notContains" ||
               op == "containsAny" || op == "containsAll" || op == "containsNone";

        private static bool IsArrayOperator(string op)
            => op == "containsAny" || op == "containsAll" || op == "containsNone";

        private static bool IsListType(string type) => type == "genres" || type == "qualities";

        private static bool IsNumericType(string type) => type == "publicationYear" || type == "pages";

        private static bool IsNumericValue(string op, JToken value)
        {
            if (op == "between")
                return value is JObject obj && IsNumber(obj["min"]) && IsNumber(obj["max"]);

            return IsNumber(value);
        }

        private static bool IsNumber(JToken token)
            => token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float);
    }
}
