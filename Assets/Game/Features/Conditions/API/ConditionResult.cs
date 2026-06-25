using System;
using System.Collections.Generic;

namespace Game.Conditions.API
{
    /// <summary>
    /// Outcome of evaluating an <see cref="ICondition"/>. Carries not just <see cref="IsMet"/> but a
    /// progress payload so UI can render "Crime 23/30" and walk composite trees via
    /// <see cref="Children"/>. <see cref="ReasonKey"/> is a stable localization key
    /// (e.g. "soldGenre.Crime", "all", "not"), never user-facing text.
    /// </summary>
    public readonly struct ConditionResult
    {
        private static readonly IReadOnlyList<ConditionResult> NoChildren = Array.Empty<ConditionResult>();

        public bool IsMet { get; }

        /// <summary>Progress numerator (e.g. books sold so far). For a composite: number of met children.</summary>
        public long Current { get; }

        /// <summary>Progress denominator (e.g. books required). For a composite: number of children needed.</summary>
        public long Target { get; }

        public string ReasonKey { get; }

        /// <summary>Per-child results for composites; empty for leaves.</summary>
        public IReadOnlyList<ConditionResult> Children { get; }

        public ConditionResult(bool isMet, long current, long target, string reasonKey,
            IReadOnlyList<ConditionResult> children = null)
        {
            IsMet = isMet;
            Current = current;
            Target = target;
            ReasonKey = reasonKey;
            Children = children ?? NoChildren;
        }

        /// <summary>Numeric leaf: met when <paramref name="current"/> &gt;= <paramref name="target"/>.</summary>
        public static ConditionResult Leaf(long current, long target, string reasonKey)
            => new ConditionResult(current >= target, current, target, reasonKey);

        /// <summary>Boolean leaf: maps to 0/1 progress so it renders uniformly with numeric ones.</summary>
        public static ConditionResult Boolean(bool met, string reasonKey)
            => new ConditionResult(met, met ? 1 : 0, 1, reasonKey);
    }
}
