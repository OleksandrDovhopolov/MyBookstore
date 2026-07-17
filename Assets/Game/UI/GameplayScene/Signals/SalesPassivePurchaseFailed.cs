namespace Game.UI
{
    public readonly struct SalesPassivePurchaseFailed
    {
        public string CustomerId { get; }
        public string CharacterId { get; }
        public string Genre { get; }

        public SalesPassivePurchaseFailed(string customerId, string characterId, string genre)
        {
            CustomerId = customerId;
            CharacterId = characterId;
            Genre = genre;
        }
    }
}
