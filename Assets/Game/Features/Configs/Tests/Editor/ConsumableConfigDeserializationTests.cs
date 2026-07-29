using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class ConsumableConfigDeserializationTests
    {
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private const string Json = @"
[
  {
    ""id"": ""fuel_canister"",
    ""displayName"": ""Fuel Canister"",
    ""descriptionKey"": ""consumable.fuel_canister.desc""
  }
]";

        [Test]
        public void Deserialize_PopulatesConsumable()
        {
            var items = JsonConvert.DeserializeObject<ConsumableConfig[]>(Json);

            Assert.IsNotNull(items);
            Assert.AreEqual(1, items.Length);
            Assert.AreEqual("fuel_canister", items[0].Id);
            Assert.AreEqual("Fuel Canister", items[0].DisplayName);
            Assert.AreEqual("consumable.fuel_canister.desc", items[0].DescriptionKey);
        }

        [Test]
        public void ConfigsService_LoadsConsumablesByConfigFileMapping_AndIndexesById()
        {
            var service = new ConfigsService(new FakeConfigSource(Json), overrides: null);
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            var all = service.GetAll<ConsumableConfig>();
            Assert.AreEqual(1, all.Count);

            var canister = service.Get<ConsumableConfig>("fuel_canister");
            Assert.IsNotNull(canister);
            Assert.AreEqual("consumable.fuel_canister.desc", canister.DescriptionKey);
        }

        [Test]
        public void Content_ConsumablesExistInBothRoots()
        {
            foreach (var root in ContentRoots)
            {
                var path = Path.Combine(root, "consumables.json");
                Assert.IsTrue(File.Exists(path), $"{path} missing.");

                var items = JsonConvert.DeserializeObject<ConsumableConfig[]>(File.ReadAllText(path));
                var canister = items.Single(i => i.Id == "fuel_canister");
                Assert.AreEqual("consumable.fuel_canister.desc", canister.DescriptionKey);
            }
        }

        [Test]
        public void Manifest_IncludesConsumables()
        {
            var manifestPath = Path.Combine("Assets", "StreamingAssets", "Configs", "manifest.json");
            var entries = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(manifestPath));

            CollectionAssert.Contains(entries, "consumables.json");
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            private readonly string _consumablesRaw;

            public FakeConfigSource(string consumablesRaw) => _consumablesRaw = consumablesRaw;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public string GetRaw(string fileName) => fileName == "consumables" ? _consumablesRaw : null;
        }
    }
}
