namespace Game.Progression.API
{
    /// <summary>
    /// Notification published after a successful AddReputation call. <see cref="Delta"/> is the
    /// effective change (after the &gt;=0 clamp), not necessarily the requested amount.
    /// </summary>
    public sealed class ProgressionChangeEvent
    {
        public int OldReputation { get; }
        public int NewReputation { get; }
        public int Delta { get; }
        public string Reason { get; }

        public ProgressionChangeEvent(int oldReputation, int newReputation, int delta, string reason)
        {
            OldReputation = oldReputation;
            NewReputation = newReputation;
            Delta = delta;
            Reason = reason;
        }
    }
}
