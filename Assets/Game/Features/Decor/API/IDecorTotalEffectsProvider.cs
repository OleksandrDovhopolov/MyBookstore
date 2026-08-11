using System.Collections.Generic;

namespace Game.Decor
{
    public interface IDecorTotalEffectsProvider
    {
        IReadOnlyList<DecorTotalEffect> GetTotalEffects(IReadOnlyList<string> activeDecorIds);
    }
}
