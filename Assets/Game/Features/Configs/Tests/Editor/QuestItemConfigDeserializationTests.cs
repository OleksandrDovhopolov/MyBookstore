using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class QuestItemConfigDeserializationTests
    {
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private const string Json = @"
[
  {
    ""id"": ""millie_letter"",
    ""displayName"": ""Millie Letter"",
    ""descriptionKey"": ""questItem.millie_letter.desc""
  }
]";

        [Test]
        public void Deserialize_PopulatesQuestItem()
        {
            var items = JsonConvert.DeserializeObject<QuestItemConfig[]>(Json);

            Assert.IsNotNull(items);
            Assert.AreEqual(1, items.Length);
            Assert.AreEqual("millie_letter", items[0].Id);
            Assert.AreEqual("Millie Letter", items[0].DisplayName);
            Assert.AreEqual("questItem.millie_letter.desc", items[0].DescriptionKey);
        }

        [Test]
        public void ConfigsService_LoadsQuestItemsByConfigFileMapping_AndIndexesById()
        {
            var service = new ConfigsService(new FakeConfigSource(Json), overrides: null);
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            var all = service.GetAll<QuestItemConfig>();
            Assert.AreEqual(1, all.Count);

            var letter = service.Get<QuestItemConfig>("millie_letter");
            Assert.IsNotNull(letter);
            Assert.AreEqual("questItem.millie_letter.desc", letter.DescriptionKey);
        }

        [Test]
        public void Content_QuestItemsExistInBothRoots()
        {
            foreach (var root in ContentRoots)
            {
                var path = Path.Combine(root, "quest_items.json");
                Assert.IsTrue(File.Exists(path), $"{path} missing.");

                var items = JsonConvert.DeserializeObject<QuestItemConfig[]>(File.ReadAllText(path));
                var letter = items.Single(i => i.Id == "millie_letter");
                Assert.AreEqual("questItem.millie_letter.desc", letter.DescriptionKey);
            }
        }

        [Test]
        public void Manifest_IncludesQuestItems()
        {
            var manifestPath = Path.Combine("Assets", "StreamingAssets", "Configs", "manifest.json");
            var entries = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(manifestPath));

            CollectionAssert.Contains(entries, "quest_items.json");
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            private readonly string _questItemsRaw;

            public FakeConfigSource(string questItemsRaw) => _questItemsRaw = questItemsRaw;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public string GetRaw(string fileName) => fileName == "quest_items" ? _questItemsRaw : null;
        }
    }
}
