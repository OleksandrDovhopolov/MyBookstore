using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// GAME-6 §Этап 5: DayConfig.scheduledDialogueIds deserializes, and the shared DayConfigLookup.ByIndex
    /// resolves days with the "exact match, else last configured" rule the sales setup providers rely on.
    /// </summary>
    public sealed class DayConfigTests
    {
        private const string Json = @"
[
  { ""id"": ""day_001"", ""dayIndex"": 1, ""title"": ""Day 1"",
    ""scheduledDialogueIds"": [""dlg_intro_tilde""] },
  { ""id"": ""day_002"", ""dayIndex"": 2, ""title"": ""Day 2"" }
]";

        [Test]
        public void Deserialize_PopulatesScheduledDialogueIds()
        {
            var days = JsonConvert.DeserializeObject<DayConfig[]>(Json);

            Assert.AreEqual(2, days.Length);
            Assert.AreEqual(new[] { "dlg_intro_tilde" }, days[0].ScheduledDialogueIds);
            // Absent field → null (a day with no scheduled dialogues).
            Assert.IsNull(days[1].ScheduledDialogueIds);
        }

        [Test]
        public void DayConfigLookup_ExactMatch_ReturnsThatDay()
        {
            var configs = LoadConfigs();

            var day = DayConfigLookup.ByIndex(configs, 1);

            Assert.IsNotNull(day);
            Assert.AreEqual("day_001", day.Id);
            Assert.AreEqual(new[] { "dlg_intro_tilde" }, day.ScheduledDialogueIds);
        }

        [Test]
        public void DayConfigLookup_NoExactMatch_FallsBackToLastConfigured()
        {
            var configs = LoadConfigs();

            // Day 99 is past the authored content → reuse the last (highest-index) configured day.
            var day = DayConfigLookup.ByIndex(configs, 99);

            Assert.IsNotNull(day);
            Assert.AreEqual("day_002", day.Id);
        }

        [Test]
        public void DayConfigLookup_NullConfigs_ReturnsNull()
        {
            Assert.IsNull(DayConfigLookup.ByIndex(null, 1));
        }

        private static IConfigsService LoadConfigs()
        {
            var service = new ConfigsService(new FakeConfigSource(Json), overrides: null);
            service.WarmupAsync(CancellationToken.None).GetAwaiter().GetResult();
            return service;
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            private readonly string _daysRaw;

            public FakeConfigSource(string daysRaw) => _daysRaw = daysRaw;

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public string GetRaw(string fileName) => fileName == "days" ? _daysRaw : null;
        }
    }
}
