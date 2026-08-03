using System;
using System.Linq;
using Game.Configs.Editor;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class ConfigSectionCatalogTests
    {
        [Test]
        public void SectionNames_AreDerivedFromConfigFileAttributes()
        {
            var sections = ConfigSectionCatalog.SectionNames.ToArray();

            CollectionAssert.Contains(sections, "books");
            CollectionAssert.Contains(sections, "bookshops");
            CollectionAssert.Contains(sections, "sample_requests");
            CollectionAssert.Contains(sections, "shop");

            CollectionAssert.DoesNotContain(sections, "requests");
            CollectionAssert.DoesNotContain(sections, "hard_requests");
            Assert.AreEqual(15, sections.Length);
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
