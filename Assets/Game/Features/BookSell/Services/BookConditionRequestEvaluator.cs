using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class BookConditionRequestEvaluator : IBookConditionRequestEvaluator
    {
        private const string LogPrefix = "[ActiveRequests]";

        private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
        {
            "genres",
            "qualities",
            "publicationYear",
            "pages"
        };

        private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
        {
            "equal",
            "notEqual",
            "greater",
            "greaterOrEqual",
            "less",
            "lessOrEqual",
            "between",
            "contains",
            "notContains",
            "containsAny",
            "containsAll",
            "containsNone"
        };

        public BookConditionEvaluation Evaluate(BookConfig book, RequestDefinitionConfig request)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!IsValid(request, out var invalidReason))
            {
                Debug.LogError($"{LogPrefix} request '{request.Id}' is invalid: {invalidReason}; treated as non-match.");
                return new BookConditionEvaluation(false, invalidReason);
            }

            var conditions = request.Conditions;
            var all = PassAll(book, conditions?.All);
            var any = PassAny(book, conditions?.Any);
            var none = PassNone(book, conditions?.None);
            return new BookConditionEvaluation(all && any && none, null);
        }

        public bool IsValid(RequestDefinitionConfig request, out string reason)
        {
            if (request == null)
            {
                reason = "request is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                reason = "id is empty";
                return false;
            }

            if (request.Conditions == null)
            {
                reason = "conditions are missing";
                return false;
            }

            if (!ValidateGroup(request.Conditions.All, out reason)) return false;
            if (!ValidateGroup(request.Conditions.Any, out reason)) return false;
            if (!ValidateGroup(request.Conditions.None, out reason)) return false;

            return true;
        }

        public string BuildDebugText(RequestDefinitionConfig request)
        {
            if (request == null) return string.Empty;
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.BookTitle))
                sb.Append(request.BookTitle).Append(": ");
            else if (!string.IsNullOrWhiteSpace(request.Genre))
                sb.Append(request.Genre).Append(": ");

            AppendGroup(sb, "ALL", request.Conditions?.All);
            AppendGroup(sb, "ANY", request.Conditions?.Any);
            AppendGroup(sb, "NONE", request.Conditions?.None);
            return sb.Length == 0 ? request.Id : sb.ToString();
        }

        private static bool ValidateGroup(RequestCondition[] group, out string reason)
        {
            reason = null;
            if (group == null) return true;

            for (var i = 0; i < group.Length; i++)
            {
                var condition = group[i];
                if (condition == null)
                {
                    reason = $"condition[{i}] is null";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(condition.Type) || !Types.Contains(condition.Type))
                {
                    reason = $"unknown type '{condition.Type}'";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(condition.Operator) || !Operators.Contains(condition.Operator))
                {
                    reason = $"unknown operator '{condition.Operator}'";
                    return false;
                }

                if (!ValidateValue(condition, out reason))
                    return false;
            }

            return true;
        }

        private static bool ValidateValue(RequestCondition condition, out string reason)
        {
            reason = null;
            var op = condition.Operator;
            var value = condition.Value;
            if (value == null || value.Type == JTokenType.Null)
            {
                reason = $"{condition.Type} {condition.Operator} has no value";
                return false;
            }

            if (IsArrayOperator(op))
            {
                if (value.Type != JTokenType.Array)
                {
                    reason = $"{condition.Type} {condition.Operator} expects an array";
                    return false;
                }
                return true;
            }

            if (string.Equals(op, "between", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Type != JTokenType.Object || value["min"] == null || value["max"] == null)
                {
                    reason = $"{condition.Type} between expects {{ min, max }}";
                    return false;
                }

                if (!TryReadNumber(value["min"], out _) || !TryReadNumber(value["max"], out _))
                {
                    reason = $"{condition.Type} between min/max must be numeric";
                    return false;
                }
                return true;
            }

            if (IsNumericType(condition.Type) && !TryReadNumber(value, out _))
            {
                reason = $"{condition.Type} {condition.Operator} expects a numeric value";
                return false;
            }

            if (IsListType(condition.Type) && value.Type == JTokenType.Array)
            {
                reason = $"{condition.Type} {condition.Operator} expects a scalar value";
                return false;
            }

            return true;
        }

        private static bool PassAll(BookConfig book, RequestCondition[] group)
        {
            if (group == null || group.Length == 0) return true;
            for (var i = 0; i < group.Length; i++)
                if (!EvaluateCondition(book, group[i])) return false;
            return true;
        }

        private static bool PassAny(BookConfig book, RequestCondition[] group)
        {
            if (group == null || group.Length == 0) return true;
            for (var i = 0; i < group.Length; i++)
                if (EvaluateCondition(book, group[i])) return true;
            return false;
        }

        private static bool PassNone(BookConfig book, RequestCondition[] group)
        {
            if (group == null || group.Length == 0) return true;
            for (var i = 0; i < group.Length; i++)
                if (EvaluateCondition(book, group[i])) return false;
            return true;
        }

        private static bool EvaluateCondition(BookConfig book, RequestCondition condition)
        {
            if (IsListType(condition.Type))
                return EvaluateList(GetList(book, condition.Type), condition.Operator, condition.Value);

            return EvaluateNumber(GetNumber(book, condition.Type), condition.Operator, condition.Value);
        }

        private static string[] GetList(BookConfig book, string type)
            => string.Equals(type, "genres", StringComparison.OrdinalIgnoreCase)
                ? book.Genres
                : book.Qualities;

        private static double GetNumber(BookConfig book, string type)
            => string.Equals(type, "publicationYear", StringComparison.OrdinalIgnoreCase)
                ? book.Published
                : book.Pages;

        private static bool EvaluateList(string[] actual, string op, JToken value)
        {
            actual ??= Array.Empty<string>();
            if (IsArrayOperator(op))
            {
                var desired = value.ToObject<string[]>() ?? Array.Empty<string>();
                if (string.Equals(op, "containsAny", StringComparison.OrdinalIgnoreCase))
                    return ContainsAny(actual, desired);
                if (string.Equals(op, "containsAll", StringComparison.OrdinalIgnoreCase))
                    return ContainsAll(actual, desired);
                if (string.Equals(op, "containsNone", StringComparison.OrdinalIgnoreCase))
                    return !ContainsAny(actual, desired);
            }

            var scalar = value.Type == JTokenType.String ? value.Value<string>() : value.ToString();
            if (string.Equals(op, "equal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, "contains", StringComparison.OrdinalIgnoreCase))
                return Contains(actual, scalar);

            if (string.Equals(op, "notEqual", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(op, "notContains", StringComparison.OrdinalIgnoreCase))
                return !Contains(actual, scalar);

            return false;
        }

        private static bool EvaluateNumber(double actual, string op, JToken value)
        {
            if (string.Equals(op, "between", StringComparison.OrdinalIgnoreCase))
            {
                var min = ReadNumber(value["min"]);
                var max = ReadNumber(value["max"]);
                return actual >= min && actual <= max;
            }

            var expected = ReadNumber(value);
            if (string.Equals(op, "equal", StringComparison.OrdinalIgnoreCase)) return NearlyEqual(actual, expected);
            if (string.Equals(op, "notEqual", StringComparison.OrdinalIgnoreCase)) return !NearlyEqual(actual, expected);
            if (string.Equals(op, "greater", StringComparison.OrdinalIgnoreCase)) return actual > expected;
            if (string.Equals(op, "greaterOrEqual", StringComparison.OrdinalIgnoreCase)) return actual >= expected;
            if (string.Equals(op, "less", StringComparison.OrdinalIgnoreCase)) return actual < expected;
            if (string.Equals(op, "lessOrEqual", StringComparison.OrdinalIgnoreCase)) return actual <= expected;
            return false;
        }

        private static bool Contains(string[] actual, string desired)
        {
            if (string.IsNullOrWhiteSpace(desired)) return false;
            for (var i = 0; i < actual.Length; i++)
                if (string.Equals(actual[i], desired, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool ContainsAny(string[] actual, string[] desired)
        {
            for (var i = 0; i < desired.Length; i++)
                if (Contains(actual, desired[i])) return true;
            return false;
        }

        private static bool ContainsAll(string[] actual, string[] desired)
        {
            for (var i = 0; i < desired.Length; i++)
                if (!Contains(actual, desired[i])) return false;
            return true;
        }

        private static bool IsListType(string type)
            => string.Equals(type, "genres", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "qualities", StringComparison.OrdinalIgnoreCase);

        private static bool IsNumericType(string type)
            => string.Equals(type, "publicationYear", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "pages", StringComparison.OrdinalIgnoreCase);

        private static bool IsArrayOperator(string op)
            => string.Equals(op, "containsAny", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(op, "containsAll", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(op, "containsNone", StringComparison.OrdinalIgnoreCase);

        private static bool TryReadNumber(JToken token, out double value)
        {
            value = 0;
            if (token == null) return false;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                value = token.Value<double>();
                return true;
            }

            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static double ReadNumber(JToken token)
            => TryReadNumber(token, out var value) ? value : 0;

        private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.0001d;

        private static void AppendGroup(StringBuilder sb, string label, RequestCondition[] group)
        {
            if (group == null || group.Length == 0) return;
            if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(" ");
            sb.Append(label).Append(": ");
            for (var i = 0; i < group.Length; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(group[i].Type).Append(' ')
                    .Append(ToSymbol(group[i].Operator)).Append(' ')
                    .Append(FormatValue(group[i].Value));
            }
        }

        private static string ToSymbol(string op)
        {
            if (string.Equals(op, "less", StringComparison.OrdinalIgnoreCase)) return "<";
            if (string.Equals(op, "lessOrEqual", StringComparison.OrdinalIgnoreCase)) return "<=";
            if (string.Equals(op, "greater", StringComparison.OrdinalIgnoreCase)) return ">";
            if (string.Equals(op, "greaterOrEqual", StringComparison.OrdinalIgnoreCase)) return ">=";
            if (string.Equals(op, "equal", StringComparison.OrdinalIgnoreCase)) return "==";
            if (string.Equals(op, "notEqual", StringComparison.OrdinalIgnoreCase)) return "!=";
            return op;
        }

        private static string FormatValue(JToken token)
        {
            if (token == null) return "null";
            if (token.Type == JTokenType.Array)
                return "[" + string.Join(", ", token.ToObject<string[]>() ?? Array.Empty<string>()) + "]";
            if (token.Type == JTokenType.Object)
                return token.ToString(Newtonsoft.Json.Formatting.None);
            return token.ToString();
        }
    }
}
