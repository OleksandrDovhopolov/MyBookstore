namespace Game.Configs.Models
{
    /// <summary>
    /// Data-driven character definition. File: characters.json (JSON array). A character is an index over
    /// story progression — the actions themselves live in Game.Quest. Memories are unlocked by their owning
    /// quest/chain reaching Awarded (derived at read time; see docs/CHARACTER_SYSTEM.md §6/§11).
    /// Configs stays feature-agnostic: no Character/Quest enums here, only ids and localization keys.
    /// </summary>
    [ConfigFile("characters")]
    public sealed class CharacterConfig : IConfig
    {
        public string Id { get; set; }

        public string DisplayNameKey { get; set; }
        public string RoleKey { get; set; }
        public string DescriptionKey { get; set; }

        /// <summary>
        /// Quests whose start (state != Pending) discovers this character — for intro/dialogue quests that
        /// have no memory. Optional; discovery also fires from any memory-linked quest. See §10/§7.
        /// </summary>
        public string[] DiscoveryQuestIds { get; set; }

        /// <summary>Quest chains whose start discovers this character. Optional. Same role as <see cref="DiscoveryQuestIds"/>.</summary>
        public string[] DiscoveryQuestChainIds { get; set; }

        public CharacterMemoryConfig[] Memories { get; set; }
    }
}
