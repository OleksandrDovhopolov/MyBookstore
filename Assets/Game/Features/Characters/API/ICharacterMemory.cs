namespace Game.Characters.API
{
    /// <summary>
    /// Read surface for a single character memory. <see cref="Unlocked"/> is derived from the owning
    /// quest/chain state (see docs/CHARACTER_SYSTEM.md §11), not persisted in Stage 1.
    /// </summary>
    public interface ICharacterMemory
    {
        string Id { get; }
        string CharacterId { get; }
        bool Unlocked { get; }
        bool IsGolden { get; }

        /// <summary>Linked quest id, or null when the memory is bound to a chain.</summary>
        string QuestId { get; }

        /// <summary>Linked chain id, or null when the memory is bound to a single quest.</summary>
        string QuestChainId { get; }
    }
}
