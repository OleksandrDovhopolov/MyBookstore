using System.Collections.Generic;

namespace Game.UI
{
    public readonly struct GameplayGenreBookCountsChanged
    {
        public IReadOnlyDictionary<string, int> Counts { get; }

        public GameplayGenreBookCountsChanged(IReadOnlyDictionary<string, int> counts)
        {
            Counts = counts;
        }
    }
}
