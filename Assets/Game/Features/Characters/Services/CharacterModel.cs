using System.Collections.Generic;
using Game.Characters.API;
using Game.Configs.Models;

namespace Game.Characters.Services
{
    /// <summary>
    /// Immutable read model for a character, assembled by <see cref="CharacterModelFactory"/> from
    /// config + saved state + quest state. Rebuildable; never a source of truth.
    /// </summary>
    internal sealed class CharacterModel : ICharacter
    {
        public CharacterModel(CharacterConfig config, bool discovered, IReadOnlyList<ICharacterMemory> memories)
        {
            Config = config;
            Discovered = discovered;
            Memories = memories;
        }

        public string Id => Config.Id;
        public bool Discovered { get; }
        public CharacterConfig Config { get; }
        public IReadOnlyList<ICharacterMemory> Memories { get; }
    }
}
