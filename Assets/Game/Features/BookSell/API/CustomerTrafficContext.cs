using System.Collections.Generic;

namespace Book.Sell.API
{
    /// <summary>
    /// API-safe snapshot of the day's inputs a <see cref="ICustomerTrafficContributor"/> may read.
    /// Deliberately a plain data bag (no reference to <c>SalesSessionSetup</c>) so external features
    /// (e.g. Game.Decor) can implement contributors without depending on the Book.Sell impl assembly.
    /// Built by the resolver from the current sales setup.
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class CustomerTrafficContext
    {
        /// <summary>Day number (matches <c>SalesSessionSetup.Day</c> / <c>DayConfig.DayIndex</c>).</summary>
        public int Day { get; }

        /// <summary>Active location id for the day (may be null in fallback setups).</summary>
        public string LocationId { get; }

        /// <summary>Ids of decor the player currently has placed for the day.</summary>
        public IReadOnlyList<string> DecorIds { get; }

        public CustomerTrafficContext(int day, string locationId, IReadOnlyList<string> decorIds)
        {
            Day = day;
            LocationId = locationId;
            DecorIds = decorIds ?? System.Array.Empty<string>();
        }
    }
}
