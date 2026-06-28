using Game.Conditions.API;
using Game.Configs.Models;

namespace Game.LocationUnlock.Services
{
    /// <summary>
    /// Turns a <see cref="LocationConfig"/> into its root <see cref="ICondition"/>:
    /// <list type="bullet">
    /// <item><c>Unlock</c> present → parse it (composite tree of conditions).</item>
    /// <item><c>Unlock</c> absent → no requirements (always-met); this is how the starting location
    /// stays open.</item>
    /// </list>
    /// Unlock is free and automatic (no cost, no player-level gate), so there is no legacy shortcut here.
    /// </summary>
    public sealed class LocationUnlockConditionBuilder
    {
        private readonly IConditionParser _parser;

        public LocationUnlockConditionBuilder(IConditionParser parser) => _parser = parser;

        public ICondition Build(LocationConfig config)
        {
            if (config == null) return new NoConditionParserFallback();

            // Parser maps a null/empty node to an always-met condition (open location).
            return _parser.Parse(config.Unlock != null && config.Unlock.HasValues ? config.Unlock : null);
        }

        // Defensive: only hit if a null config slips through; never-met keeps it closed.
        private sealed class NoConditionParserFallback : ICondition
        {
            public ConditionResult Evaluate() => ConditionResult.Boolean(false, "invalid.location");
        }
    }
}
