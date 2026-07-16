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

        /// <summary>
        /// Optional. When set and the quest is Active, a quest character arrives in Sales carrying this
        /// dialogue — once (GAME-6). Quest-aware customer spawners read it via <c>IQuest.Config</c>;
        /// the dialogue engine resolves the graph from dialogues.json by this id. Fire-once is tracked
        /// separately (delivered-dialogues store), not by quest state.
        /// </summary>
        public string DialogueId { get; set; }

        public string TitleKey { get; set; }
        public string DescriptionKey { get; set; }

        /// <summary>MVP: 0 or 1 element (linear chain). Branching is a future graph, not a QuestChain.</summary>
        public string[] NextQuestIds { get; set; }

        public QuestTaskConfig[] Tasks { get; set; }

        /// <summary>
        /// Optional authored passive purchase attempts for the quest customer. The sales spawner maps this
        /// config data onto the spawned customer; the shared passive step still owns feedback/commit.
        ///
        /// This is the beat sheet of the SAME one-time encounter <see cref="DialogueId"/> declares — the two
        /// fields describe one scripted visit ("who shows up and talks" + "what they do"), which is why they
        /// live together. Fire-once comes from the same place: the delivered-dialogues store keyed by
        /// <see cref="DialogueId"/>, so the script cannot replay while the quest stays Active.
        ///
        /// Deliberately a TEMPORARY anchor: both fields migrate to a dedicated customer-script config
        /// (docs/INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md "Candidate E") once a SECOND scripted
        /// encounter exists — one instance does not justify the new noun. See TODO GAME-16.
        /// </summary>
        public ScriptedPassivePurchaseConfig[] ScriptedPassivePurchases { get; set; }

        /// <summary>Condition tree that activates the quest (Pending → Active). Null/empty = auto-active.</summary>
        public JObject ActivationConditions { get; set; }

        /// <summary>Condition tree that fails the quest. Usually null.</summary>
        public JObject FailConditions { get; set; }

        public QuestRewardConfig[] Rewards { get; set; }

        public QuestWorldEffectConfig[] WorldEffects { get; set; }
    }
}
