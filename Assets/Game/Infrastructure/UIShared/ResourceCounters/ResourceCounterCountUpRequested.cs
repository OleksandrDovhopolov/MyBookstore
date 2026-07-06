namespace UIShared
{
    public readonly struct ResourceCounterCountUpRequested
    {
        public string ResourceId { get; }
        public float DurationSeconds { get; }

        public ResourceCounterCountUpRequested(string resourceId, float durationSeconds)
        {
            ResourceId = resourceId;
            DurationSeconds = durationSeconds;
        }
    }
}
