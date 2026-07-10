namespace Game.Quest.API
{
    /// <summary>
    /// Lifecycle state of a quest. Mirrors the prod heroes model adapted for cozy flow
    /// (see docs/QUESTS.md §5): Pending → Active → ReadyToAward → Awarded, with Failed as a side exit.
    /// </summary>
    public enum QuestState
    {
        /// <summary>Waiting for activation conditions; not persisted (default).</summary>
        Pending = -1,

        /// <summary>Activated; tasks are being evaluated.</summary>
        Active = 0,

        /// <summary>All tasks completed; awaiting the award step (final dialog / reward grant).</summary>
        ReadyToAward = 1,

        /// <summary>Rewards and permanent effects have been applied.</summary>
        Awarded = 2,

        /// <summary>Quest failed (rare; e.g. a missed one-shot event).</summary>
        Failed = 3
    }
}
