namespace UIShared
{
    public readonly struct ResourceCounterCountUpRequested
    {
        public string ResourceId { get; }

        public ResourceCounterCountUpRequested(string resourceId)
        {
            ResourceId = resourceId;
        }
    }
}
