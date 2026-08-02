using System.IO;
using System.Linq;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Rewards.API;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class EconomyConfigDeserializationTests
    {
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private const string Json = @"
[
  {
    ""id"": ""economy"",
    ""baseSaleChance"": 0.05,
    ""perCopyChance"": 0.05,
    ""capChance"": 0.50,
    ""locationDemandMultiplier"": 1.5,
    ""dayCompletionRewards"": [
      { ""id"": ""postcard"", ""category"": ""consumable"", ""amount"": 1, ""kind"": ""InventoryItem"" }
    ]
  }
]";

        [Test]
        public void Deserialize_PopulatesDayCompletionRewards()
        {
            var configs = JsonConvert.DeserializeObject<EconomyConfig[]>(Json);

            Assert.IsNotNull(configs);
            Assert.AreEqual(1, configs.Length);

            var economy = configs[0];
            Assert.AreEqual(EconomyConfig.SingletonId, economy.Id);
            Assert.IsNotNull(economy.DayCompletionRewards);
            Assert.AreEqual(1, economy.DayCompletionRewards.Length);

            var reward = economy.DayCompletionRewards[0];
            Assert.AreEqual("postcard", reward.Id);
            Assert.AreEqual(InventoryCategories.Consumable, reward.Category);
            Assert.AreEqual(1, reward.Amount);
            Assert.AreEqual(RewardKind.InventoryItem, reward.Kind);
        }

        [Test]
        public void Content_Economy_GrantsPostcardForCompletedDay()
        {
            foreach (var root in ContentRoots)
            {
                var configs = JsonConvert.DeserializeObject<EconomyConfig[]>(
                    File.ReadAllText(Path.Combine(root, "economy.json")));
                var economy = configs.Single(c => c.Id == EconomyConfig.SingletonId);
                var reward = economy.DayCompletionRewards.Single(r => r.Id == "postcard");

                Assert.AreEqual(InventoryCategories.Consumable, reward.Category);
                Assert.AreEqual(1, reward.Amount);
                Assert.AreEqual(RewardKind.InventoryItem, reward.Kind);
            }
        }
    }
}
