using Game.Conditions.API;
using Game.Configs.Models;
using UnityEngine;

namespace Game.LocationUnlock.Services
{
    /// <summary>
    /// Turns a <see cref="LocationConfig"/> into its root <see cref="ICondition"/>, encoding the
    /// legacy-vs-new policy in one place (single source of truth):
    /// <list type="bullet">
    /// <item><c>Unlock</c> present → parse it; if <c>RequiredLevel</c> is also set, warn and ignore it.</item>
    /// <item><c>Unlock</c> absent, <c>RequiredLevel</c> &gt; 0 → there is no player-level provider yet, so
    /// it cannot be enforced; treated as no-requirement (open) with a one-time warning rather than
    /// silently locking existing content. Revisit when a level provider lands.</item>
    /// <item>Neither → no requirements (always unlockable).</item>
    /// </list>
    /// </summary>
    public sealed class LocationUnlockConditionBuilder
    {
        private const string LogPrefix = "[LocationUnlock]";

        private readonly IConditionParser _parser;

        public LocationUnlockConditionBuilder(IConditionParser parser) => _parser = parser;

        public ICondition Build(LocationConfig config)
        {
            if (config == null) return new NoConditionParserFallback();

            if (config.Unlock != null && config.Unlock.HasValues)
            {
                if (config.RequiredLevel > 0)
                    Debug.LogWarning($"{LogPrefix} location '{config.Id}' sets both 'unlock' and legacy " +
                                     $"RequiredLevel={config.RequiredLevel}; RequiredLevel is ignored (unlock wins).");
                return _parser.Parse(config.Unlock);
            }

            if (config.RequiredLevel > 0)
                Debug.LogWarning($"{LogPrefix} location '{config.Id}' uses legacy RequiredLevel=" +
                                 $"{config.RequiredLevel} but there is no player-level provider yet; " +
                                 $"treated as no requirement. Migrate to an 'unlock' playerLevel condition.");

            // No 'unlock' node → no requirements. Parser maps a null node to an always-met condition.
            return _parser.Parse(null);
        }

        // Defensive: only hit if a null config slips through; never-met keeps it closed.
        private sealed class NoConditionParserFallback : ICondition
        {
            public ConditionResult Evaluate() => ConditionResult.Boolean(false, "invalid.location");
        }
    }
}
