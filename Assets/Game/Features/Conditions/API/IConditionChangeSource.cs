using System;

namespace Game.Conditions.API
{
    /// <summary>
    /// Optional contract for <see cref="IConditionFactory"/> implementations whose leaves read runtime state.
    /// Pull consumers subscribe to <see cref="Changed"/> so newly changed state is re-evaluated without
    /// hard-coding each feature domain.
    /// </summary>
    public interface IConditionChangeSource
    {
        event Action Changed;
    }
}
