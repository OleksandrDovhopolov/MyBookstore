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

        public CharacterMemoryConfig[] Memories { get; set; }
    }
}
