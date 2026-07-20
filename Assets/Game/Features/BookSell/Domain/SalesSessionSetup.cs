using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// "What the player brought into the day": which location was picked and which books
    /// are on the shelf. Once the Preparation phase exists, the setup will come from there.
    /// For now it is assembled by the fallback provider from the first location + first N books.
    /// </summary>
    public sealed class SalesSessionSetup
    {
        public int Day { get; }
        public string LocationId { get; }
        public IReadOnlyList<string> ShelfBookIds { get; }
        public IReadOnlyList<int> WaveSizes { get; }
        public float WaveGapSeconds { get; }

        /// <summary>Decor does not affect scoring yet (out of scope), but the field is preserved for future integration.</summary>
        public IReadOnlyList<string> DecorIds { get; }

        public SalesSessionSetup(
            int day,
            string locationId,
            IReadOnlyList<string> shelfBookIds,
            IReadOnlyList<string> decorIds = null,
            IReadOnlyList<int> waveSizes = null,
            float waveGapSeconds = 0f)
        {
            Day = day;
            LocationId = locationId;
            ShelfBookIds = shelfBookIds ?? System.Array.Empty<string>();
            DecorIds = decorIds ?? System.Array.Empty<string>();
            WaveSizes = waveSizes;
            WaveGapSeconds = waveGapSeconds;
        }
    }
}
