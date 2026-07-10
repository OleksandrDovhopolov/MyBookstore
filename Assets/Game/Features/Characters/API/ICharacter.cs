using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.Characters.API
{
    /// <summary>
    /// Read surface for a character: static config plus derived runtime state (discovered + memories).
    /// Rebuilt from CharacterConfig + saved state + IQuestsService; never a source of truth itself.
    /// </summary>
    public interface ICharacter
    {
        string Id { get; }
        bool Discovered { get; }
        CharacterConfig Config { get; }
        IReadOnlyList<ICharacterMemory> Memories { get; }
    }
}
