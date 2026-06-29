using Game.Characters.API;

namespace Game.Characters.Services
{
    /// <summary>Immutable read model for one memory, assembled by <see cref="CharacterModelFactory"/>.</summary>
    internal sealed class CharacterMemory : ICharacterMemory
    {
        public CharacterMemory(string id, string characterId, bool unlocked, bool isGolden,
            string questId, string questChainId)
        {
            Id = id;
            CharacterId = characterId;
            Unlocked = unlocked;
            IsGolden = isGolden;
            QuestId = questId;
            QuestChainId = questChainId;
        }

        public string Id { get; }
        public string CharacterId { get; }
        public bool Unlocked { get; }
        public bool IsGolden { get; }
        public string QuestId { get; }
        public string QuestChainId { get; }
    }
}
