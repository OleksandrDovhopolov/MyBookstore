using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class CustomerScriptConfigDeserializationTests
    {
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

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
    ""dayIndex"": 1,
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
            Assert.IsTrue(eddi.DayIndex.HasValue);
            Assert.AreEqual(1, eddi.DayIndex.Value);
            Assert.IsNull(eddi.ActivationQuestId);
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
            Assert.IsTrue(eddi.DayIndex.HasValue);
            Assert.AreEqual(1, eddi.DayIndex.Value);
            Assert.IsNull(eddi.ActivationQuestId);
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
            foreach (var root in ContentRoots)
                AssertDayOneUsesTwoWavesForEddiThenMissNpc(root);
        }

        [Test]
        public void Content_BundledDefaults_MatchAuthoringConfigs()
        {
            var authoringRoot = Path.Combine("Assets", "Configs");
            var bundledRoot = Path.Combine("Assets", "StreamingAssets", "Configs");

            foreach (var sourcePath in Directory.GetFiles(authoringRoot, "*.json"))
            {
                var fileName = Path.GetFileName(sourcePath);
                var bundledPath = Path.Combine(bundledRoot, fileName);

                Assert.IsTrue(File.Exists(bundledPath), $"Bundled config '{fileName}' is missing.");
                Assert.AreEqual(
                    NormalizeJson(File.ReadAllText(sourcePath)),
                    NormalizeJson(File.ReadAllText(bundledPath)),
                    $"Bundled config '{fileName}' is out of sync with Assets/Configs. Run Tools/Configs/Sync Bundled Defaults to StreamingAssets before building.");
            }
        }

        [Test]
        public void Content_FirstDayStarterGenres_CoverScriptedPassiveAttempts()
        {
            foreach (var root in ContentRoots)
                AssertFirstDayStarterGenresCoverScriptedPassiveAttempts(root);
        }

        private static void AssertDayOneUsesTwoWavesForEddiThenMissNpc(string root)
        {
            var days = JsonConvert.DeserializeObject<DayConfig[]>(
                File.ReadAllText(Path.Combine(root, "days.json")));

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
            foreach (var root in ContentRoots)
                AssertDayOneMissScriptUsesKnownTravelGenre(root);
        }

        private static void AssertDayOneMissScriptUsesKnownTravelGenre(string root)
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(
                File.ReadAllText(Path.Combine(root, "customer_scripts.json")));
            var books = JsonConvert.DeserializeObject<BookConfig[]>(
                File.ReadAllText(Path.Combine(root, "books.json")));

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
            foreach (var root in ContentRoots)
                AssertEddiScriptUsesQuestDialogueAndKnownGenres(root);
        }

        private static void AssertEddiScriptUsesQuestDialogueAndKnownGenres(string root)
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(
                File.ReadAllText(Path.Combine(root, "customer_scripts.json")));
            var books = JsonConvert.DeserializeObject<BookConfig[]>(
                File.ReadAllText(Path.Combine(root, "books.json")));

            var script = scripts.Single(s => s.Id == "eddi_intro");
            Assert.IsTrue(script.DayIndex.HasValue);
            Assert.AreEqual(1, script.DayIndex.Value);
            Assert.IsNull(script.ActivationQuestId);
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

        private static void AssertFirstDayStarterGenresCoverScriptedPassiveAttempts(string root)
        {
            var scripts = JsonConvert.DeserializeObject<CustomerScriptConfig[]>(
                File.ReadAllText(Path.Combine(root, "customer_scripts.json")));
            var books = JsonConvert.DeserializeObject<BookConfig[]>(
                File.ReadAllText(Path.Combine(root, "books.json")));

            var genreCounts = books
                .Where(b => !string.IsNullOrEmpty(b?.PrimaryGenre))
                .GroupBy(b => b.PrimaryGenre, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            AssertGenreCount("Fantasy", 5);
            AssertGenreCount("Fact", 3);
            AssertGenreCount("Travel", 3);
            AssertGenreCount("Kids", 2);

            var scriptedDayOneAttempts = scripts
                .Where(s => s.DayIndex == 1 || string.Equals(s.Id, "eddi_intro", StringComparison.Ordinal))
                .SelectMany(s => s.PassiveAttempts ?? Array.Empty<ScriptedPassivePurchaseConfig>());

            foreach (var attempt in scriptedDayOneAttempts.Where(a => a.ForceHit))
            {
                Assert.IsTrue(
                    genreCounts.TryGetValue(attempt.Genre, out var count) && count > 0,
                    $"Scripted forced hit genre '{attempt.Genre}' must have starter stock in {root}.");
            }

            void AssertGenreCount(string genre, int min)
            {
                Assert.IsTrue(
                    genreCounts.TryGetValue(genre, out var count) && count >= min,
                    $"{root}/books.json must include at least {min} primary '{genre}' book(s) for the FTUE starter preset.");
            }
        }

        private static string NormalizeJson(string json)
            => JToken.Parse(json).ToString(Formatting.None);

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
