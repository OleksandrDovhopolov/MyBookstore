namespace Game.Resources.API
{
    /// <summary>
    /// Notification published after a successful Add/Remove on <see cref="IResourcesService"/>.
    /// <see cref="Delta"/> is signed (positive on Add, negative on Remove); <see cref="Reason"/> is
    /// audit text (e.g. "ftue", "results_day_3") that is logged and forwarded to subscribers but
    /// does not affect the operation itself.
    /// </summary>
    public sealed class ResourceChangeEvent
    {
        public string ResourceId { get; }
        public int OldAmount { get; }
        public int NewAmount { get; }
        public int Delta { get; }
        public string Reason { get; }

        public ResourceChangeEvent(string resourceId, int oldAmount, int newAmount, int delta, string reason)
        {
            ResourceId = resourceId;
            OldAmount = oldAmount;
            NewAmount = newAmount;
            Delta = delta;
            Reason = reason;
        }
    }
}
