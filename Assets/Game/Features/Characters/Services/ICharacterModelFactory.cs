using Game.Characters.API;
using Game.Characters.Services.Persistence;
using Game.Configs.Models;

namespace Game.Characters.Services
{
    /// <summary>
    /// Pure assembler of character read models from config + saved state + quest state. Does not mutate
    /// save, subscribe to events, unlock memories or activate quests (docs/CHARACTER_SYSTEM.md §7).
    /// </summary>
    internal interface ICharacterModelFactory
    {
        CharacterModel Create(CharacterConfig config, SavedCharacter saved);
        CharacterJournalEntry CreateJournalEntry(CharacterConfig config, SavedCharacter saved);
    }
}
