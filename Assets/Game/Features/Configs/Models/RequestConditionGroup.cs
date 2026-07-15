namespace Game.Configs.Models
{
    /// <summary>
    /// Flat boolean grouping of conditions. A book matches when:
    /// <c>(all pass) AND (any passes) AND (none pass)</c>.
    /// Each group is independent; a null or empty group is neutral (evaluates to true), so a request
    /// specifies only the groups it needs.
    ///
    /// <para>Nested groups are intentionally NOT supported here. For the rare request that cannot be
    /// expressed with flat groups, use the escape-hatch (raw conditions JSON) rather than growing this
    /// structure — see docs/INPROGRESS/ACTIVE_REQUEST_CONDITIONS.md.</para>
    /// </summary>
    public sealed class RequestConditionGroup
    {
        /// <summary>Every condition must pass (logical AND). Null/empty ⇒ neutral (true).</summary>
        public RequestCondition[] All { get; set; }

        /// <summary>At least one condition must pass (logical OR). Null/empty ⇒ neutral (true).</summary>
        public RequestCondition[] Any { get; set; }

        /// <summary>No condition may pass (logical NOR / exclusion). Null/empty ⇒ neutral (true).</summary>
        public RequestCondition[] None { get; set; }
    }
}
