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
    ""dayIndex"": 1,
    ""characterId"": null,
    ""passiveAttempts"": [
      { ""genre"": ""Travel"", ""forceHit"": false }
    ]
  },
  {
    ""id"": ""eddi_intro"",
    ""activationQuestId"": ""q_intro_eddi"",
    ""characterId"": ""eddi"",
    ""dialogueId"": ""eddy1"",
    ""passiveAttempts"": [
      { ""genre"": ""Fact"", ""forceHit"": true },
      { ""genre"": ""Travel"", ""forceHit"": false }
    ]
  }
]";

        [Test]
        public void Deserialize_PopulatesCustomerScript()
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(Json);

            Assert.IsNotNull(scripts);
            Assert.AreEqual(2, scripts.Length);

            var script = scripts[0];
            Assert.AreEqual("day2_missed_sale", script.Id);
            Assert.IsTrue(script.DayIndex.HasValue);
            Assert.AreEqual(1, script.DayIndex.Value);
            Assert.IsNull(script.ActivationQuestId);
            Assert.IsNull(script.DialogueId);
            Assert.IsNull(script.CharacterId);
            Assert.AreEqual(1, script.PassiveAttempts.Length);
            Assert.AreEqual("Travel", script.PassiveAttempts[0].Genre);
            Assert.IsFalse(script.PassiveAttempts[0].ForceHit);

            var eddi = scripts[1];
            Assert.IsFalse(eddi.DayIndex.HasValue);
            Assert.AreEqual("q_intro_eddi", eddi.ActivationQuestId);
            Assert.AreEqual("eddy1", eddi.DialogueId);
            Assert.AreEqual("eddi", eddi.CharacterId);
            Assert.AreEqual(2, eddi.PassiveAttempts.Length);
        }

        [Test]
        public void ConfigsService_LoadsCustomerScriptsByConfigFileMapping_AndIndexesById()
        {
            var service = new ConfigsService(new FakeConfigSource(Json), overrides: null);
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();

            var all = service.GetAll<CustomerScriptConfig>();
            Assert.AreEqual(2, all.Count);

            var script = service.Get<CustomerScriptConfig>("day2_missed_sale");
            Assert.IsNotNull(script, "Resolved by Id -> [ConfigFile] + lazy load + indexing all wired.");
            Assert.IsTrue(script.DayIndex.HasValue);
            Assert.AreEqual(1, script.DayIndex.Value);

            var eddi = service.Get<CustomerScriptConfig>("eddi_intro");
            Assert.IsNotNull(eddi);
            Assert.AreEqual("q_intro_eddi", eddi.ActivationQuestId);
        }

        [Test]
        public void Deserialize_PopulatesDayWaves()
        {
            const string daysJson = @"[
  {
    ""id"": ""day_001"",
    ""dayIndex"": 1,
    ""customerCount"": 4,
    ""activeRequestCount"": 0,
    ""waveSizes"": [1, 3],
    ""waveGapSeconds"": 3,
    ""applyModifiers"": false
  }
]";

            var days = JsonConvert.DeserializeObject<DayConfig[]>(daysJson);
            var day1 = days.Single(d => d.DayIndex == 1);

            CollectionAssert.AreEqual(new[] { 1, 3 }, day1.WaveSizes);
            Assert.IsTrue(day1.WaveGapSeconds.HasValue);
            Assert.AreEqual(3f, day1.WaveGapSeconds.Value);
        }

        [Test]
        public void Content_DayOne_UsesTwoWavesForEddiThenMissNpc()
        {
            var days = JsonConvert.DeserializeObject<DayConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "days.json")));

            var day1 = days.Single(d => d.DayIndex == 1);
            Assert.IsTrue(day1.CustomerCount.HasValue);
            Assert.AreEqual(4, day1.CustomerCount.Value);
            Assert.IsTrue(day1.ActiveRequestCount.HasValue);
            Assert.AreEqual(0, day1.ActiveRequestCount.Value);
            CollectionAssert.AreEqual(new[] { 1, 3 }, day1.WaveSizes);
            Assert.IsTrue(day1.WaveGapSeconds.HasValue);
            Assert.AreEqual(3f, day1.WaveGapSeconds.Value);
            Assert.IsTrue(day1.ApplyModifiers.HasValue);
            Assert.IsFalse(day1.ApplyModifiers.Value);
        }

        [Test]
        public void Content_DayOneMissScript_UsesKnownTravelGenre()
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "customer_scripts.json")));
            var books = JsonConvert.DeserializeObject<BookConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "books.json")));

            var script = scripts.Single(s => s.Id == "day2_missed_sale");
            Assert.IsTrue(script.DayIndex.HasValue);
            Assert.AreEqual(1, script.DayIndex.Value);

            var attempt = AssertOneAttempt(script);
            Assert.AreEqual("Travel", attempt.Genre);
            Assert.IsFalse(attempt.ForceHit);

            Assert.IsTrue(
                books.Any(b => string.Equals(b.PrimaryGenre, attempt.Genre, StringComparison.OrdinalIgnoreCase)),
                "The scripted miss genre must exist in BookConfig.PrimaryGenre.");
        }

        [Test]
        public void Content_EddiScript_UsesQuestDialogueAndKnownGenres()
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "customer_scripts.json")));
            var books = JsonConvert.DeserializeObject<BookConfig[]>(
                File.ReadAllText(Path.Combine("Assets", "Configs", "books.json")));

            var script = scripts.Single(s => s.Id == "eddi_intro");
            Assert.IsFalse(script.DayIndex.HasValue);
            Assert.AreEqual("q_intro_eddi", script.ActivationQuestId);
            Assert.AreEqual("eddi", script.CharacterId);
            Assert.AreEqual("eddy1", script.DialogueId);
            Assert.AreEqual(2, script.PassiveAttempts.Length);

            CollectionAssert.AreEqual(
                new[] { "Fact", "Travel" },
                script.PassiveAttempts.Select(a => a.Genre).ToArray());
            Assert.IsTrue(script.PassiveAttempts[0].ForceHit);
            Assert.IsFalse(script.PassiveAttempts[1].ForceHit);

            foreach (var attempt in script.PassiveAttempts)
            {
                Assert.IsTrue(
                    books.Any(b => string.Equals(b.PrimaryGenre, attempt.Genre, StringComparison.OrdinalIgnoreCase)),
                    $"The scripted Eddi genre '{attempt.Genre}' must exist in BookConfig.PrimaryGenre.");
            }
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
