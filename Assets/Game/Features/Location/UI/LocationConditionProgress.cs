namespace Game.Location.UI
{
    /// <summary>
    /// One unlock requirement flattened for the row UI: which genre (icon), current vs. target count,
    /// raw condition reason, and whether it is already met. <see cref="SpriteId"/> is optional:
    /// some condition keys are abstract progress counters, not Addressables sprite ids.
    /// </summary>
    public readonly struct LocationConditionProgress
    {
        public string Genre { get; }
        public string ReasonKey { get; }
        public string SpriteId { get; }
        public long Current { get; }
        public long Target { get; }
        public bool IsMet { get; }

        public LocationConditionProgress(
            string genre,
            long current,
            long target,
            bool isMet,
            string spriteId = null,
            string reasonKey = null)
        {
            Genre = genre;
            ReasonKey = reasonKey;
            SpriteId = spriteId;
            Current = current;
            Target = target;
            IsMet = isMet;
        }
    }
}
