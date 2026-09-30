using System;
using System.IO;
using System.Linq;
using Game.Configs.Editor;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class ConfigSectionCatalogTests
    {
        private static readonly string ConfigsDir = Path.Combine("Assets", "Configs");

        private const string LocalizationPrefix = "localization_";

        [Test]
        public void SectionNames_AreDerivedFromConfigFileAttributes()
        {
            var sections = ConfigSectionCatalog.SectionNames.ToArray();

            CollectionAssert.Contains(sections, "books");
            CollectionAssert.Contains(sections, "bookshops");
            CollectionAssert.Contains(sections, "sample_requests");
            CollectionAssert.Contains(sections, "shelf_presets");
            CollectionAssert.Contains(sections, "shop");

            CollectionAssert.DoesNotContain(sections, "requests");
            CollectionAssert.DoesNotContain(sections, "hard_requests");
            CollectionAssert.DoesNotContain(sections, "popular_books");
        }

        /// <summary>
        /// The invariant a section count used to stand in for: authored content and declared sections
        /// describe the same set. A hardcoded number rots every time a section lands (it sat at 16 while
        /// request_phrases made 17) and never caught the real mistakes: a json with no model shipping as
        /// dead weight, or a model with no json to load.
        /// </summary>
        [Test]
        public void SectionNames_MatchAuthoredConfigFiles()
        {
            Assert.IsTrue(Directory.Exists(ConfigsDir), $"{ConfigsDir} not found.");

            var sections = ConfigSectionCatalog.SectionNames.ToArray();
            var authored = Directory.GetFiles(ConfigsDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !name.StartsWith(LocalizationPrefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var orphanFiles = authored
                .Where(name => !sections.Contains(name, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            Assert.IsEmpty(
                orphanFiles,
                $"{ConfigsDir} ships json with no [ConfigFile] model: {string.Join(", ", orphanFiles)}");

            var sectionsWithoutFile = sections
                .Where(section => !authored.Contains(section, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            Assert.IsEmpty(
                sectionsWithoutFile,
                $"[ConfigFile] sections with no json in {ConfigsDir}: {string.Join(", ", sectionsWithoutFile)}");
        }

        [Test]
        public void SectionNames_AreUniqueAndSorted()
        {
            var sections = ConfigSectionCatalog.SectionNames.ToArray();
            var distinct = sections.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var sorted = sections.OrderBy(section => section, StringComparer.OrdinalIgnoreCase).ToArray();

            CollectionAssert.AreEqual(distinct, sections);
            CollectionAssert.AreEqual(sorted, sections);
        }

        [Test]
        public void IsKnownSection_UsesCaseInsensitiveLookup()
        {
            Assert.IsTrue(ConfigSectionCatalog.IsKnownSection("books"));
            Assert.IsTrue(ConfigSectionCatalog.IsKnownSection("SAMPLE_REQUESTS"));
            Assert.IsFalse(ConfigSectionCatalog.IsKnownSection("hard_requests"));
            Assert.IsFalse(ConfigSectionCatalog.IsKnownSection(null));
        }
    }
}
