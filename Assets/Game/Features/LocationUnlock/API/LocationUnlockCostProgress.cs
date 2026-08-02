namespace Game.LocationUnlock.API
{
    /// <summary>One item requirement for manually unlocking a location.</summary>
    public readonly struct LocationUnlockCostProgress
    {
        public string ItemId { get; }
        public int Have { get; }
        public int Need { get; }
        public bool IsMet => Have >= Need;

        public LocationUnlockCostProgress(string itemId, int have, int need)
        {
            ItemId = itemId;
            Have = have;
            Need = need;
        }
    }
}
