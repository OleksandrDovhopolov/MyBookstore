using System.IO;
using System.Linq;
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
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private const string Json = @"
[
  {
    ""id"": ""far_beach_intro"",
    ""type"": ""story"",
    ""chainId"": ""far_beach_sand_empire"",
    ""characterId"": ""eddi"",
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
            Assert.AreEqual("eddi", intro.CharacterId);
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

        [Test]
        public void Content_EddiIntro_IsDialogueDeliveredSalesQuestWithFuelReward()
        {
            foreach (var root in ContentRoots)
                AssertEddiIntroQuest(root);
        }

        [Test]
        public void Content_MillieIntro_IsDialogueDeliveredActivePickQuestWithLetterAndFuelReward()
        {
            foreach (var root in ContentRoots)
                AssertMillieIntroQuest(root);
        }

        [Test]
        public void Content_TaraIntro_IsDialogueDeliveredActivePickQuestWithPortReward()
        {
            foreach (var root in ContentRoots)
                AssertTaraIntroQuest(root);
        }

        [Test]
        public void Content_MillieDiscovery_UsesIntroQuest()
        {
            foreach (var root in ContentRoots)
            {
                var characters = JsonConvert.DeserializeObject<CharacterConfig[]>(
                    File.ReadAllText(Path.Combine(root, "characters.json")));
                var millie = characters.Single(c => c.Id == "millie");

                CollectionAssert.Contains(millie.DiscoveryQuestIds, "q_intro_millie");
            }
        }

        [Test]
        public void Content_QuestCatalog_HasFourCharacterLinkedQuests_InBothRoots()
        {
            foreach (var root in ContentRoots)
            {
                var quests = JsonConvert.DeserializeObject<QuestConfig[]>(
                    File.ReadAllText(Path.Combine(root, "quests.json")));
                var characters = JsonConvert.DeserializeObject<CharacterConfig[]>(
                    File.ReadAllText(Path.Combine(root, "characters.json")));

                Assert.AreEqual(4, quests.Length, root);

                foreach (var quest in quests)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(quest.CharacterId), quest.Id);
                    Assert.IsTrue(characters.Any(c => c.Id == quest.CharacterId),
                        $"{quest.Id} characterId '{quest.CharacterId}' must resolve in characters.json");
                    Assert.IsTrue(characters.Any(c => c.DiscoveryQuestIds != null
                        && c.DiscoveryQuestIds.Contains(quest.Id)),
                        $"{quest.Id} must be referenced by a character discoveryQuestIds entry");
                }
            }
        }

        [Test]
        public void Content_StoryCharacters_HaveQuestLinkedMemories_InBothRoots()
        {
            foreach (var root in ContentRoots)
            {
                var quests = JsonConvert.DeserializeObject<QuestConfig[]>(
                    File.ReadAllText(Path.Combine(root, "quests.json")));
                var characters = JsonConvert.DeserializeObject<CharacterConfig[]>(
                    File.ReadAllText(Path.Combine(root, "characters.json")));
                var questIds = new System.Collections.Generic.HashSet<string>(quests.Select(q => q.Id));

                AssertStoryCharacterMemory(characters, questIds, "eddi");
                AssertStoryCharacterMemory(characters, questIds, "millie");
                AssertStoryCharacterMemory(characters, questIds, "tara");
                AssertStoryCharacterMemory(characters, questIds, "captain");
            }
        }

        private static void AssertEddiIntroQuest(string root)
        {
            var quests = JsonConvert.DeserializeObject<QuestConfig[]>(
                File.ReadAllText(Path.Combine(root, "quests.json")));

            var quest = quests.Single(q => q.Id == "q_intro_eddi");
            Assert.AreEqual("story", quest.Type);
            Assert.AreEqual("eddi", quest.CharacterId);
            AssertDialogueDeliveredActivation(quest, "eddy1");

            Assert.AreEqual(4, quest.Tasks.Length);
            AssertSalesTask(quest.Tasks[0], 1, "Crime", 10);
            AssertSalesTask(quest.Tasks[1], 2, "Drama", 10);
            AssertSalesTask(quest.Tasks[2], 3, "Classic", 10);
            AssertSalesTask(quest.Tasks[3], 4, "Fantasy", 15);
            Assert.AreEqual(4, quest.Tasks.Select(t => t.Id).Distinct().Count());

            Assert.AreEqual(1, quest.Rewards.Length);
            Assert.AreEqual("InventoryItem", quest.Rewards[0].Kind);
            Assert.AreEqual("fuel_canister", quest.Rewards[0].Id);
            Assert.AreEqual("consumable", quest.Rewards[0].Category);
            Assert.AreEqual(2, quest.Rewards[0].Amount);
        }

        private static void AssertMillieIntroQuest(string root)
        {
            var quests = JsonConvert.DeserializeObject<QuestConfig[]>(
                File.ReadAllText(Path.Combine(root, "quests.json")));

            var quest = quests.Single(q => q.Id == "q_intro_millie");
            Assert.AreEqual("story", quest.Type);
            Assert.AreEqual("millie", quest.CharacterId);
            AssertDialogueDeliveredActivation(quest, "millie1");

            Assert.AreEqual(1, quest.Tasks.Length);
            var task = quest.Tasks[0];
            Assert.AreEqual(1, task.Id);
            Assert.IsNotNull(task.CompletionConditions);
            Assert.AreEqual("activePickGenre", task.CompletionConditions["type"].ToString());
            Assert.AreEqual("Fact", task.CompletionConditions["genre"].ToString());
            Assert.AreEqual(5, (int)task.CompletionConditions["min"]);

            Assert.AreEqual(2, quest.Rewards.Length);
            var letter = quest.Rewards.Single(r => r.Id == "millie_letter");
            Assert.AreEqual("InventoryItem", letter.Kind);
            Assert.AreEqual("quest_item", letter.Category);
            Assert.AreEqual(1, letter.Amount);

            var fuel = quest.Rewards.Single(r => r.Id == "fuel_canister");
            Assert.AreEqual("InventoryItem", fuel.Kind);
            Assert.AreEqual("consumable", fuel.Category);
            Assert.AreEqual(1, fuel.Amount);
        }

        private static void AssertTaraIntroQuest(string root)
        {
            var quests = JsonConvert.DeserializeObject<QuestConfig[]>(
                File.ReadAllText(Path.Combine(root, "quests.json")));

            var quest = quests.Single(q => q.Id == "q_tara_kids");
            Assert.AreEqual("story", quest.Type);
            Assert.AreEqual("tara", quest.CharacterId);
            AssertDialogueDeliveredActivation(quest, "tara_quest_1");

            Assert.AreEqual(1, quest.Tasks.Length);
            var task = quest.Tasks[0];
            Assert.AreEqual(1, task.Id);
            Assert.IsNotNull(task.CompletionConditions);
            Assert.AreEqual("activePickGenre", task.CompletionConditions["type"].ToString());
            Assert.AreEqual("Kids", task.CompletionConditions["genre"].ToString());
            Assert.AreEqual(5, (int)task.CompletionConditions["min"]);

            var permit = quest.Rewards.Single(r => r.Id == "port_trade_permit");
            Assert.AreEqual("InventoryItem", permit.Kind);
            Assert.AreEqual("quest_item", permit.Category);
            Assert.AreEqual(1, permit.Amount);
        }

        private static void AssertDialogueDeliveredActivation(QuestConfig quest, string dialogueId)
        {
            Assert.IsNotNull(quest.ActivationConditions);
            Assert.AreEqual("dialogueDelivered", quest.ActivationConditions["type"].ToString());
            Assert.AreEqual(dialogueId, quest.ActivationConditions["dialogueId"].ToString());
        }

        private static void AssertStoryCharacterMemory(
            CharacterConfig[] characters,
            System.Collections.Generic.HashSet<string> questIds,
            string characterId)
        {
            var character = characters.Single(c => c.Id == characterId);
            Assert.IsNotNull(character.Memories, characterId);
            Assert.IsTrue(character.Memories.Length > 0, characterId);

            foreach (var memory in character.Memories)
            {
                Assert.IsFalse(string.IsNullOrEmpty(memory.Id), characterId);
                Assert.IsFalse(string.IsNullOrEmpty(memory.QuestId), memory.Id);
                Assert.IsTrue(questIds.Contains(memory.QuestId),
                    $"{characterId}.{memory.Id} questId '{memory.QuestId}' must resolve in quests.json");
            }
        }

        private static void AssertSalesTask(QuestTaskConfig task, int id, string genre, int min)
        {
            Assert.AreEqual(id, task.Id);
            Assert.IsNotNull(task.CompletionConditions);
            Assert.AreEqual("soldGenre", task.CompletionConditions["type"].ToString());
            Assert.AreEqual(genre, task.CompletionConditions["genre"].ToString());
            Assert.AreEqual(min, (int)task.CompletionConditions["min"]);
        }
    }
}
