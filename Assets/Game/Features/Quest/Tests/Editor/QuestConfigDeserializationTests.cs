using Game.Configs.Models;
using Game.Quest.API;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Quest.Tests.Editor
{
    /// <summary>
    /// API-stage coverage: the QuestConfig DTO graph deserializes from JSON, condition trees survive as
    /// raw JObject, and the enum helpers behave. No ConditionParser here — parsing is verified in the
    /// implementation stage (this assembly doesn't reference Game.Conditions/Game.SalesStats).
    /// </summary>
    public sealed class QuestConfigDeserializationTests
    {
        private const string Json = @"
[
  {
    ""id"": ""far_beach_intro"",
    ""type"": ""story"",
    ""chainId"": ""far_beach_sand_empire"",
    ""characterId"": null,
    ""titleKey"": ""quest.far_beach_intro.title"",
    ""descriptionKey"": ""quest.far_beach_intro.desc"",
    ""nextQuestIds"": [""sand_inspiration""],
    ""tasks"": [
      {
        ""id"": 1,
        ""descriptionKey"": ""task.visit_far_beach"",
        ""completionConditions"": { ""all"": [ { ""type"": ""visitLocation"", ""locationId"": ""far_beach"", ""min"": 3 } ] }
      }
    ],
    ""activationConditions"": null,
    ""rewards"": [],
    ""worldEffects"": []
  },
  {
    ""id"": ""sand_inspiration"",
    ""type"": ""side"",
    ""chainId"": ""far_beach_sand_empire"",
    ""nextQuestIds"": [],
    ""tasks"": [
      {
        ""id"": 1,
        ""descriptionKey"": ""task.sell_fantasy_far_beach"",
        ""completionConditions"": { ""all"": [ { ""type"": ""soldGenreAtLocation"", ""genre"": ""Fantasy"", ""locationId"": ""far_beach"", ""min"": 15 } ] }
      }
    ],
    ""rewards"": [ { ""kind"": ""InventoryItem"", ""id"": ""harper_castle_donation_box"", ""category"": ""decor"", ""amount"": 1 } ],
    ""worldEffects"": [ { ""type"": ""locationCustomerBonus"", ""params"": { ""locationId"": ""far_beach"", ""amount"": 2 } } ]
  }
]";

        [Test]
        public void Deserialize_PopulatesQuestGraph()
        {
            var quests = JsonConvert.DeserializeObject<QuestConfig[]>(Json);

            Assert.IsNotNull(quests);
            Assert.AreEqual(2, quests.Length);

            var intro = quests[0];
            Assert.AreEqual("far_beach_intro", intro.Id);
            Assert.AreEqual("story", intro.Type);
            Assert.AreEqual("far_beach_sand_empire", intro.ChainId);
            Assert.IsNull(intro.CharacterId);
            Assert.AreEqual(new[] { "sand_inspiration" }, intro.NextQuestIds);
            Assert.IsNull(intro.ActivationConditions);
            Assert.AreEqual(1, intro.Tasks.Length);
            Assert.AreEqual(1, intro.Tasks[0].Id);
        }

        [Test]
        public void Deserialize_PreservesConditionJObjectStructurally()
        {
            var quests = JsonConvert.DeserializeObject<QuestConfig[]>(Json);
            var task = quests[1].Tasks[0];

            Assert.IsNotNull(task.CompletionConditions);
            var leaf = task.CompletionConditions["all"][0];
            Assert.AreEqual("soldGenreAtLocation", leaf["type"].ToString());
            Assert.AreEqual("Fantasy", leaf["genre"].ToString());
            Assert.AreEqual("far_beach", leaf["locationId"].ToString());
            Assert.AreEqual(15, (int)leaf["min"]);
        }

        [Test]
        public void Deserialize_PopulatesRewardsAndWorldEffects()
        {
            var quests = JsonConvert.DeserializeObject<QuestConfig[]>(Json);
            var sand = quests[1];

            Assert.AreEqual(1, sand.Rewards.Length);
            Assert.AreEqual("InventoryItem", sand.Rewards[0].Kind);
            Assert.AreEqual("harper_castle_donation_box", sand.Rewards[0].Id);
            Assert.AreEqual(1, sand.Rewards[0].Amount);

            Assert.AreEqual(1, sand.WorldEffects.Length);
            Assert.AreEqual("locationCustomerBonus", sand.WorldEffects[0].Type);
            Assert.AreEqual(2, (int)sand.WorldEffects[0].Params["amount"]);
        }

        [Test]
        public void QuestType_RoundTrips_AndParsesCaseInsensitively()
        {
            Assert.AreEqual("story", QuestType.Story.ToConfigValue());
            Assert.AreEqual("side", QuestType.Side.ToConfigValue());
            Assert.AreEqual("tutorial", QuestType.Tutorial.ToConfigValue());

            Assert.IsTrue(QuestTypeExtensions.TryParse("Story", out var t1));
            Assert.AreEqual(QuestType.Story, t1);
            Assert.IsTrue(QuestTypeExtensions.TryParse("story", out var t2));
            Assert.AreEqual(QuestType.Story, t2);

            Assert.IsFalse(QuestTypeExtensions.TryParse("nonsense", out _));
            Assert.IsFalse(QuestTypeExtensions.TryParse(null, out _));
        }

        [Test]
        public void StateExtensions_TruthTable()
        {
            Assert.IsTrue(QuestState.Awarded.IsCompleted());
            Assert.IsTrue(QuestState.ReadyToAward.IsCompleted());
            Assert.IsFalse(QuestState.Active.IsCompleted());
            Assert.IsFalse(QuestState.Pending.IsCompleted());
            Assert.IsFalse(QuestState.Failed.IsCompleted());

            Assert.IsTrue(QuestTaskState.Completed.IsClosed());
            Assert.IsTrue(QuestTaskState.Failed.IsClosed());
            Assert.IsFalse(QuestTaskState.Active.IsClosed());
            Assert.IsFalse(QuestTaskState.Pending.IsClosed());
        }
    }
}
