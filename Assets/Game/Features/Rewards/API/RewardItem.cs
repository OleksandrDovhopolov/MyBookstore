namespace Game.Rewards.API
{
    /// <summary>
    /// One atomic payload entry inside a <see cref="RewardSpec"/>. For <see cref="RewardKind.Resource"/>
    /// the <see cref="Category"/> is unused; for <see cref="RewardKind.InventoryItem"/> it maps to
    /// <c>Game.Inventory.API.InventoryCategories</c>.
    /// </summary>
    public readonly struct RewardItem
    {
        public string Id { get; }
        public string Category { get; }
        public int Amount { get; }
        public RewardKind Kind { get; }

        public RewardItem(string id, string category, int amount, RewardKind kind)
        {
            Id = id;
            Category = category;
            Amount = amount;
            Kind = kind;
        }

        public static RewardItem Resource(string id, int amount) =>
            new RewardItem(id, null, amount, RewardKind.Resource);

        public static RewardItem InventoryItem(string id, string category, int amount) =>
            new RewardItem(id, category, amount, RewardKind.InventoryItem);
    }
}
