namespace UIShared
{
    public readonly struct ResourceCounterDisplayOverrideChanged
    {
        public string ResourceId { get; }
        public int Amount { get; }
        public bool Active { get; }

        public ResourceCounterDisplayOverrideChanged(string resourceId, int amount, bool active)
        {
            ResourceId = resourceId;
            Amount = amount;
            Active = active;
        }
    }
}
