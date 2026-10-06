using System;
using System.IO;
using System.Linq;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// Inventory book rows are genre aggregates built in code, so their description keys are built
    /// from the enum rather than authored on a config field. LocalizationKeyValidator only sees keys
    /// referenced from config fields, which makes this test the only guard against a genre row
    /// rendering a raw "[book_genre.x.desc]" on screen.
    /// </summary>
    public sealed class BookGenreDescriptionKeyTests
    {
        private static readonly string ItemsLocalizationPath =
            Path.Combine("Assets", "Configs", "localization_items_en.json");

        [Test]
        public void EveryGenre_HasADescriptionKeyInItemsLocalization()
        {
            var table = JObject.Parse(File.ReadAllText(ItemsLocalizationPath));

            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
            {
                var key = genre.ToDescriptionLocalizationKey();
                var value = (string)table[key];

                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(value),
                    $"Missing or empty localization key '{key}' in {ItemsLocalizationPath}.");
            }
        }

        [Test]
        public void DescriptionKeys_AreLowercaseAndDistinct()
        {
            var keys = Enum.GetValues(typeof(BookGenre))
                .Cast<BookGenre>()
                .Select(genre => genre.ToDescriptionLocalizationKey())
                .ToArray();

            // Localization lookups are Ordinal, and every genre-scoped key in the project is lowercase.
            Assert.IsTrue(keys.All(key => key == key.ToLowerInvariant()), "Keys must be lowercase.");
            Assert.AreEqual(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        }
    }
}
