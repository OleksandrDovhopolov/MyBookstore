namespace Game.Configs.Models
{
    /// <summary>
    /// A single memory belonging to a <see cref="CharacterConfig"/>. In Tiny Bookshop a memory is the
    /// photograph shown for a completed NPC challenge. It unlocks when its linked quest (<see cref="QuestId"/>)
    /// or the final quest of its chain (<see cref="QuestChainId"/>) reaches Awarded.
    /// <see cref="IsGolden"/> is a UI-significance flag only — rewards still flow through Game.Quest.
    /// </summary>
    public sealed class CharacterMemoryConfig
    {
        public string Id { get; set; }

        public string TitleKey { get; set; }
        public string DescriptionKey { get; set; }

        /// <summary>Asset key for the memory photograph shown in the Journal.</summary>
        public string PhotoKey { get; set; }

        /// <summary>Unlocks when this specific quest is Awarded. Mutually exclusive with <see cref="QuestChainId"/>.</summary>
        public string QuestId { get; set; }

        /// <summary>Unlocks when the final quest of this chain is Awarded. Mutually exclusive with <see cref="QuestId"/>.</summary>
        public string QuestChainId { get; set; }

        public bool IsGolden { get; set; }
    }
}
