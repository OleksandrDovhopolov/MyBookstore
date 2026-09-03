using Game.Decor;

namespace Game.Configs.Models
{
    /// <summary>
    /// A decoration that the player can place in a shop slot. File: decors.json (JSON array).
    /// Phase 0 mechanics use only <see cref="GenreMultipliers"/>; all other fields are data-only
    /// reserved for future phases (see docs/INPROGRESS/Decor.md §6).
    /// </summary>
    [ConfigFile("decors")]
    public sealed class DecorConfig : IConfig
    {
        public string Id { get; set; }
        public string DisplayNameKey { get; set; }
        //TODO now everywhere used decor id for sprite loading via addressabbles . should delete this ? 
        public string IconAddress { get; set; }

        // Placement constraints
        public DecorPositionType PositionType { get; set; }
        public DecorSize Size { get; set; }

        // Economy
        public int BasePrice { get; set; }

        /// <summary>
        /// Signed shift to a location's per-visit entry fee while this decor is active (gold).
        /// Neutral = 0; negative discounts, positive raises. Distinct from <see cref="BasePrice"/>.
        /// See docs/SAVE_DAY_FLOW.md.
        /// </summary>
        public int VisitCostDelta { get; set; }

        // Gameplay effect (Phase 0: only field with runtime effect)
        public DecorGenreModifier[] GenreMultipliers { get; set; }

        /// <summary>
        /// Additive percent modifier this decor applies to the day's regular customer count while placed.
        /// Neutral = 0 (e.g. +0.10 = +10% visitors). Distinct from <see cref="GenreMultipliers"/> (sale chance).
        /// Consumed by the customer traffic resolver (see docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md).
        /// </summary>
        public float CustomerTrafficPercentDelta { get; set; }

        // Reserved (Phase 0: data-only)
        public string[] AtmosphereTags { get; set; }
    }
}
