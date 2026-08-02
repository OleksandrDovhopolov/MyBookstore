namespace Game.Location.UI
{
    /// <summary>
    /// One unlock requirement flattened for the row UI: which genre (icon), current vs. target count,
    /// and whether it is already met. <see cref="SpriteId"/> is optional: some condition keys are
    /// abstract progress counters, not Addressables sprite ids.
    /// </summary>
    public readonly struct LocationConditionProgress
    {
        public string Genre { get; }
        public string SpriteId { get; }
        public long Current { get; }
        public long Target { get; }
        public bool IsMet { get; }

        public LocationConditionProgress(string genre, long current, long target, bool isMet, string spriteId = null)
        {
            Genre = genre;
            SpriteId = spriteId;
            Current = current;
            Target = target;
            IsMet = isMet;
        }
    }
}
