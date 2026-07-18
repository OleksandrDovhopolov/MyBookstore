using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class CustomerScriptConfigDeserializationTests
    {
        private const string Json = @"
[
  {
    ""id"": ""day2_missed_sale"",
    ""dayIndex"": 2,
    ""characterId"": null,
    ""passiveAttempts"": [
      { ""genre"": ""Travel"", ""forceHit"": false }
    ]
  }
]";

        [Test]
        public void Deserialize_PopulatesCustomerScript()
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(Json);

            Assert.IsNotNull(scripts);
            Assert.AreEqual(1, scripts.Length);

            var script = scripts[0];
            Assert.AreEqual("day2_missed_sale", script.Id);
            Assert.AreEqual(2, script.DayIndex);
            Assert.IsNull(script.CharacterId);
            Assert.AreEqual(1, script.PassiveAttempts.Length);
            Assert.AreEqual("Travel", script.PassiveAttempts[0].Genre);
            Assert.IsFalse(script.PassiveAttempts[0].ForceHit);
        }

        [Test]
        public void ConfigsService_LoadsCustomerScriptsByConfigFileMapping_AndIndexesById()
        {
            var service = new ConfigsService(new FakeConfigSource(Json), overrides: null);
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            var all = service.GetAll<CustomerScriptConfig>();
            Assert.AreEqual(1, all.Count);

            var script = service.Get<CustomerScriptConfig>("day2_missed_sale");
            Assert.IsNotNull(script, "Resolved by Id -> [ConfigFile] + lazy load + indexing all wired.");
            Assert.AreEqual(2, script.DayIndex);
        }

        [Test]
        public void Content_DayTwo_IsThreePassiveOnlyCustomers()
        {
            var days = JsonConvert.DeserializeObject<DayConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "days.json")));

            var day2 = days.Single(d => d.DayIndex == 2);
            Assert.IsTrue(day2.CustomerCount.HasValue);
            Assert.AreEqual(3, day2.CustomerCount.Value);
            Assert.IsTrue(day2.ActiveRequestCount.HasValue);
            Assert.AreEqual(0, day2.ActiveRequestCount.Value);
            Assert.IsTrue(day2.ApplyModifiers.HasValue);
            Assert.IsFalse(day2.ApplyModifiers.Value);
        }

        [Test]
        public void Content_DayTwoScript_UsesKnownTravelGenre()
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "customer_scripts.json")));
            var books = JsonConvert.DeserializeObject<BookConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "books.json")));

            var script = scripts.Single(s => s.Id == "day2_missed_sale");
            Assert.AreEqual(2, script.DayIndex);

            var attempt = AssertOneAttempt(script);
            Assert.AreEqual("Travel", attempt.Genre);
            Assert.IsFalse(attempt.ForceHit);

            Assert.IsTrue(
                books.Any(b => string.Equals(b.PrimaryGenre, attempt.Genre, StringComparison.OrdinalIgnoreCase)),
                "The scripted miss genre must exist in BookConfig.PrimaryGenre.");
        }

        private static ScriptedPassivePurchaseConfig AssertOneAttempt(CustomerScriptConfig script)
        {
            Assert.IsNotNull(script.PassiveAttempts);
            Assert.AreEqual(1, script.PassiveAttempts.Length);
            return script.PassiveAttempts[0];
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            private readonly string _customerScriptsRaw;

            public FakeConfigSource(string customerScriptsRaw) => _customerScriptsRaw = customerScriptsRaw;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public string GetRaw(string fileName) => fileName == "customer_scripts" ? _customerScriptsRaw : null;
        }
    }
}
