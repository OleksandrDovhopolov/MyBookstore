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

        /// <summary>Decor does not affect scoring yet (out of scope), but the field is preserved for future integration.</summary>
        public IReadOnlyList<string> DecorIds { get; }

        /// <summary>Quest-character dialogues scheduled for this day (from <c>DayConfig.ScheduledDialogueIds</c>,
        /// GAME-6 §Этап 5). The decorator spawner prepends a quest character per id; empty on ordinary days.</summary>
        public IReadOnlyList<string> ScheduledDialogueIds { get; }

        public SalesSessionSetup(
            int day,
            string locationId,
            IReadOnlyList<string> shelfBookIds,
            IReadOnlyList<string> decorIds = null,
            IReadOnlyList<string> scheduledDialogueIds = null)
        {
            Day = day;
            LocationId = locationId;
            ShelfBookIds = shelfBookIds ?? System.Array.Empty<string>();
            DecorIds = decorIds ?? System.Array.Empty<string>();
            ScheduledDialogueIds = scheduledDialogueIds ?? System.Array.Empty<string>();
        }
    }
}
