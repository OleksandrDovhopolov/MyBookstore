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
        public string DisplayName { get; set; }
        //TODO now everywhere used decor id for sprite loading via addressabbles . should delete this ? 
        public string IconAddress { get; set; }

        // Placement constraints
        public DecorPositionType PositionType { get; set; }
        public DecorSize Size { get; set; }

        // Economy
        public int BasePrice { get; set; }
        public DecorRarity Rarity { get; set; }

        // Gameplay effect (Phase 0: only field with runtime effect)
        public DecorGenreModifier[] GenreMultipliers { get; set; }

        // Reserved (Phase 0: data-only)
        public string[] Styles { get; set; }
        public string[] AtmosphereTags { get; set; }
        public bool Paintable { get; set; }
        public bool Electric { get; set; }
        public bool Activatable { get; set; }
        public bool Distracting { get; set; }
        public int DailyUpkeepCost { get; set; }
    }
}
