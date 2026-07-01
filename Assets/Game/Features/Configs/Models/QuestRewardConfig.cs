namespace Game.Configs.Models
{
    /// <summary>
    /// One reward entry granted when a quest reaches Awarded. Mapped to <c>Game.Rewards.API.RewardItem</c>
    /// at runtime (mapping lives in the Quest feature, not in Configs).
    /// </summary>
    public sealed class QuestRewardConfig
    {
        /// <summary>"Resource" | "InventoryItem" — maps to Game.Rewards.API.RewardKind.</summary>
        public string Kind { get; set; }

        public string Id { get; set; }

        /// <summary>Inventory category; unused for resources.</summary>
        public string Category { get; set; }

        public int Amount { get; set; }
    }
}
