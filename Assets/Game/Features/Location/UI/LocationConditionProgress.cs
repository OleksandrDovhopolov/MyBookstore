namespace Game.Location.UI
{
    /// <summary>
    /// One unlock requirement flattened for the row UI: which genre (icon), current vs. target count,
    /// and whether it is already met. <see cref="Genre"/> is the sprite/Addressables id (e.g. "Crime").
    /// </summary>
    public readonly struct LocationConditionProgress
    {
        public string Genre { get; }
        public long Current { get; }
        public long Target { get; }
        public bool IsMet { get; }

        public LocationConditionProgress(string genre, long current, long target, bool isMet)
        {
            Genre = genre;
            Current = current;
            Target = target;
            IsMet = isMet;
        }
    }
}
