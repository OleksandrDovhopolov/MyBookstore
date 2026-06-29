using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Characters.API;
using Game.Characters.Services.Persistence;
using Game.Configs;
using Game.Configs.Models;
using Game.Quest.API;
using Save;
using UnityEngine;

namespace Game.Characters.Services
{
    /// <summary>
    /// Read-side <see cref="ICharactersService"/>. Mirrors <c>QuestsService</c>: registers as
    /// <see cref="ISaveHook"/> only for init timing — the catalog is built in <see cref="AfterLoadAsync"/>
    /// (configs are warm by then). Models are rebuilt per read from CharacterConfig + saved state +
    /// <see cref="IQuestsService"/>, so discovered/memory state always reflects current quest state.
    ///
    /// Stage 1 is event-free and write-free: <see cref="BeforeSaveAsync"/> is a no-op and the
    /// discovery/memory events are never raised (that is Stage 2).
    /// </summary>
    public sealed class CharactersService : ICharactersService, ISaveHook
    {
        private const string LogPrefix = "[Characters]";

        private readonly IConfigsService _configs;
        private readonly ICharactersRepository _repository; // null → in-memory only (tests)
        private readonly ICharacterModelFactory _factory;

        private readonly Dictionary<string, CharacterConfig> _configsById = new(StringComparer.Ordinal);
        private SavedCharacters _saved = new();

        public CharactersService(
            ISaveService save,
            IConfigsService configs,
            IQuestsService quests,
            ICharactersRepository repository = null)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (quests == null) throw new ArgumentNullException(nameof(quests));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _repository = repository;
            // Internal assembler kept out of DI — depends only on the already-injected IQuestsService.
            _factory = new CharacterModelFactory(quests);

            save.RegisterHook(this);
        }

#pragma warning disable 67 // Declared for the public contract; raised in Stage 2 (quest-event wiring).
        public event Action<ICharacter> CharacterDiscovered;
        public event Action<ICharacterMemory> MemoryUnlocked;
#pragma warning restore 67

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            BuildCatalog();
            if (_repository != null)
                _saved = await _repository.LoadAsync(ct) ?? new SavedCharacters();
            Debug.Log($"{LogPrefix} loaded: {_configsById.Count} characters.");
        }

        // Stage 1: no character-owned writes (discovery is derived / Stage 2). No-op like QuestsService.
        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- ICharactersService (read) -----

        public ICharacter TryGetCharacter(string characterId)
            => characterId != null && _configsById.TryGetValue(characterId, out var config)
                ? _factory.Create(config, GetSaved(characterId))
                : null;

        public IEnumerable<ICharacter> GetAllCharacters()
        {
            var result = new List<ICharacter>(_configsById.Count);
            foreach (var config in _configsById.Values)
                result.Add(_factory.Create(config, GetSaved(config.Id)));
            return result;
        }

        public IEnumerable<ICharacter> GetDiscoveredCharacters()
        {
            var result = new List<ICharacter>();
            foreach (var config in _configsById.Values)
            {
                var model = _factory.Create(config, GetSaved(config.Id));
                if (model.Discovered) result.Add(model);
            }
            return result;
        }

        public bool IsDiscovered(string characterId)
            => TryGetCharacter(characterId)?.Discovered ?? false;

        public bool IsMemoryUnlocked(string characterId, string memoryId)
        {
            var character = TryGetCharacter(characterId);
            if (character == null || memoryId == null) return false;

            foreach (var memory in character.Memories)
                if (string.Equals(memory.Id, memoryId, StringComparison.Ordinal))
                    return memory.Unlocked;

            return false;
        }

        public CharacterJournalEntry GetJournalEntry(string characterId)
            => characterId != null && _configsById.TryGetValue(characterId, out var config)
                ? _factory.CreateJournalEntry(config, GetSaved(characterId))
                : null;

        // ----- internals -----

        private void BuildCatalog()
        {
            _configsById.Clear();
            foreach (var config in _configs.GetAll<CharacterConfig>())
            {
                if (config?.Id == null) continue;
                _configsById[config.Id] = config;
            }
        }

        private SavedCharacter GetSaved(string characterId)
            => _saved?.Characters != null && _saved.Characters.TryGetValue(characterId, out var s) ? s : null;
    }
}
