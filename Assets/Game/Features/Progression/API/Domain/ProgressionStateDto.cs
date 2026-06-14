namespace Game.Progression.API
{
    /// <summary>
    /// Transport DTO between <see cref="IProgressionService"/> and <see cref="IProgressionRepository"/>.
    /// Holds the player's reputation. Future progression stats (levels, achievements unlocks) live
    /// in this same DTO.
    /// </summary>
    public sealed class ProgressionStateDto
    {
        public int Reputation { get; set; }
    }
}
