using Newtonsoft.Json.Linq;

namespace Game.Configs.Models
{
    /// <summary>
    /// Data-driven quest definition. File: quests.json (JSON array). Conditions are raw
    /// <see cref="JObject"/> trees (same pattern as <see cref="LocationConfig.Unlock"/>) parsed later by
    /// the Conditions engine — Configs stays feature-agnostic, so <see cref="Type"/> is a string and
    /// there are no Quest enums here.
    /// </summary>
    [ConfigFile("quests")]
    public sealed class QuestConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>"story" | "side" | "tutorial" — parsed via QuestTypeExtensions.</summary>
        public string Type { get; set; }

        public string ChainId { get; set; }

        /// <summary>Owning character; null until the characters feature exists.</summary>
        public string CharacterId { get; set; }

        public string TitleKey { get; set; }
        public string DescriptionKey { get; set; }

        /// <summary>MVP: 0 or 1 element (linear chain). Branching is a future graph, not a QuestChain.</summary>
        public string[] NextQuestIds { get; set; }

        public QuestTaskConfig[] Tasks { get; set; }

        /// <summary>Condition tree that activates the quest (Pending → Active). Null/empty = auto-active.</summary>
        public JObject ActivationConditions { get; set; }

        /// <summary>Condition tree that fails the quest. Usually null.</summary>
        public JObject FailConditions { get; set; }

        public QuestRewardConfig[] Rewards { get; set; }

        public QuestWorldEffectConfig[] WorldEffects { get; set; }
    }
}
