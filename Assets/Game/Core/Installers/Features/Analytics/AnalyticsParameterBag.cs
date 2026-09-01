using System.Collections.Generic;

namespace Game.Bootstrap.Analytics
{
    internal static class AnalyticsParameterBag
    {
        public static void AddString(Dictionary<string, object> parameters, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters[key] = value;
            }
        }
    }
}
