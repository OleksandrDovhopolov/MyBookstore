using Game.Conditions.API;

namespace Game.Conditions.Services
{
    /// <summary>Always met. Used when a target has no requirements at all (null/empty unlock node).</summary>
    public sealed class AlwaysMetCondition : ICondition
    {
        public const string ReasonKey = "always";
        public static readonly AlwaysMetCondition Instance = new();

        private AlwaysMetCondition() { }

        public ConditionResult Evaluate() => ConditionResult.Boolean(true, ReasonKey);
    }

    /// <summary>
    /// Never met. Fail-closed result for an unknown condition type or a malformed node, so a config
    /// typo locks the content (and logs an error) instead of crashing or silently unlocking.
    /// </summary>
    public sealed class NeverMetCondition : ICondition
    {
        private readonly string _reasonKey;

        public NeverMetCondition(string reasonKey) => _reasonKey = reasonKey;

        public ConditionResult Evaluate() => ConditionResult.Boolean(false, _reasonKey);
    }
}
