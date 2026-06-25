namespace Game.Conditions.API
{
    /// <summary>
    /// A single requirement — either a leaf (reads one data provider) or a composite
    /// (<c>AllOf</c> / <c>AnyOf</c> / <c>Not</c> over child conditions). Evaluation is a synchronous
    /// pure read of already-loaded state, like <c>IProgressionService.Reputation</c>; it must not do
    /// I/O. Domain-agnostic: the engine knows nothing about locations, sales, fishing, etc.
    /// </summary>
    public interface ICondition
    {
        ConditionResult Evaluate();
    }
}
