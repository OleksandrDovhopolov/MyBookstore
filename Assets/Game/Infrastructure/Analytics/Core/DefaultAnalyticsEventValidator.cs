using System;
using System.Text.RegularExpressions;

namespace Analytics
{
    public sealed class DefaultAnalyticsEventValidator : IAnalyticsEventValidator
    {
        private static readonly Regex NameRegex = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

        private readonly IAnalyticsConfig _config;

        public DefaultAnalyticsEventValidator(IAnalyticsConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool Validate(IAnalyticsEvent analyticsEvent, out string error)
        {
            if (analyticsEvent == null)
            {
                error = "Analytics event is null.";
                return false;
            }

            if (!ValidateName(analyticsEvent.Name, _config.MaxEventNameLength, "Event name", out error))
            {
                return false;
            }

            if (analyticsEvent.Parameters == null)
            {
                error = "Event parameters dictionary is null.";
                return false;
            }

            if (analyticsEvent.Parameters.Count > _config.MaxParameterCount)
            {
                error = $"Parameter count exceeds limit {_config.MaxParameterCount}.";
                return false;
            }

            foreach (var parameter in analyticsEvent.Parameters)
            {
                if (!ValidateName(parameter.Key, _config.MaxParameterKeyLength, "Parameter key", out error))
                {
                    return false;
                }

                if (parameter.Value == null)
                {
                    error = $"Parameter '{parameter.Key}' has null value.";
                    return false;
                }

                if (!IsSupportedValue(parameter.Value))
                {
                    error = $"Parameter '{parameter.Key}' has unsupported value type {parameter.Value.GetType().Name}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateName(string value, int maxLength, string label, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"{label} is empty.";
                return false;
            }

            if (maxLength > 0 && value.Length > maxLength)
            {
                error = $"{label} exceeds limit {maxLength}.";
                return false;
            }

            if (!NameRegex.IsMatch(value))
            {
                error = $"{label} '{value}' does not match snake_case format.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool IsSupportedValue(object value)
        {
            return value is string or int or long or float or double or bool;
        }
    }
}
