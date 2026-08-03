using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Game.Configs.Editor
{
    /// <summary>
    /// Editor-only catalog of config sections declared by <see cref="ConfigFileAttribute"/>.
    /// </summary>
    public static class ConfigSectionCatalog
    {
        private static readonly Lazy<string[]> CachedSectionNames = new(BuildSectionNames);

        public static IReadOnlyList<string> SectionNames => CachedSectionNames.Value;

        public static bool IsKnownSection(string section)
        {
            if (string.IsNullOrWhiteSpace(section)) return false;
            return CachedSectionNames.Value.Contains(section, StringComparer.OrdinalIgnoreCase);
        }

        private static string[] BuildSectionNames()
            => TypeCache.GetTypesDerivedFrom<IConfig>()
                .Where(type => type != null
                               && type.Assembly == typeof(IConfig).Assembly
                               && type.IsClass
                               && !type.IsAbstract)
                .Select(type => Attribute.GetCustomAttribute(type, typeof(ConfigFileAttribute)) as ConfigFileAttribute)
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute?.FileName))
                .Select(attribute => attribute.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
