using System.IO;
using System.Linq;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class ShelfPresetConfigDeserializationTests
    {
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private const string Json = @"
[
  {
    ""id"": ""preset_test"",
    ""displayName"": ""Test preset"",
    ""bookIds"": [""book01"", ""book02""]
  }
]";

        [Test]
        public void Deserialize_PopulatesPresetFields()
        {
            var presets = JsonConvert.DeserializeObject<ShelfPresetConfig[]>(Json);

            Assert.IsNotNull(presets);
            Assert.AreEqual(1, presets.Length);
            Assert.AreEqual("preset_test", presets[0].Id);
            Assert.AreEqual("Test preset", presets[0].DisplayName);
            CollectionAssert.AreEqual(new[] { "book01", "book02" }, presets[0].BookIds);
        }

        [Test]
        public void Content_DefaultPreset_IsBundledInBothRoots()
        {
            foreach (var root in ContentRoots)
            {
                var presets = JsonConvert.DeserializeObject<ShelfPresetConfig[]>(
                    File.ReadAllText(Path.Combine(root, "shelf_presets.json")));
                var preset = presets.Single(p => p.Id == "preset_default_42");

                Assert.AreEqual("Default 7x6", preset.DisplayName);
                Assert.AreEqual(42, preset.BookIds.Length);
            }
        }
    }
}
