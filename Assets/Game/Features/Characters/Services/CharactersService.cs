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
    /// <see cref="ICharactersService"/> integrated with the quest lifecycle (Stage 2). Registers as
    /// <see cref="ISaveHook"/>: builds the catalog in <see cref="AfterLoadAsync"/>, reconciles state against
    /// current quest state (seeding without raising events), then subscribes to <see cref="IQuestsService"/>.
    ///
    /// Discovery and memory unlock are routed by a reverse index (questId/chainId → characterId) built from
    /// the character configs, so Game.Quest needs no character-aware API. Memory unlock in the read model is
    /// quest-derived OR the persisted ledger; discovered is the persisted flag. State is persisted via
    /// <see cref="BeforeSaveAsync"/>; the service unsubscribes on <see cref="Dispose"/>.
    /// </summary>
    public sealed class CharactersService : ICharactersService, ISaveHook, IDisposable
    {
        private const string LogPrefix = "[Characters]";

        private readonly ISaveService _save;
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;
        private readonly ICharactersRepository _repository; // null → in-memory only (tests)
        private readonly ICharacterModelFactory _factory;

        private readonly Dictionary<string, CharacterConfig> _configsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _questToCharacter = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _chainToCharacter = new(StringComparer.Ordinal);

        private SavedCharacters _saved = new();
        private bool _dirty;
        private bool _subscribed;

        public CharactersService(
            ISaveService save,
            IConfigsService configs,
            IQuestsService quests,
            ICharactersRepository repository = null)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _repository = repository;
            _factory = new CharacterModelFactory(quests);

            save.RegisterHook(this);
        }

        public event Action<ICharacter> CharacterDiscovered;
        public event Action<ICharacterMemory> MemoryUnlocked;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            BuildCatalog();
            if (_repository != null)
                _saved = await _repository.LoadAsync(ct) ?? new SavedCharacters();
            Reconcile();   // seed discovered/ledger from current quest state without raising events
            Subscribe();
            Debug.Log($"{LogPrefix} loaded: {_configsById.Count} characters.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct)
        {
            if (_repository == null || !_dirty) return UniTask.CompletedTask;
            _dirty = false;
            return _repository.SaveAsync(_saved, ct);
        }

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

        // ----- quest integration -----

        private void Subscribe()
        {
            if (_subscribed) return;
            _quests.QuestStarted += OnQuestEvent;
            _quests.QuestAwarded += OnQuestEvent;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _quests.QuestStarted -= OnQuestEvent;
            _quests.QuestAwarded -= OnQuestEvent;
            _subscribed = false;
        }

        private void OnQuestEvent(IQuest quest)
        {
            var characterId = ResolveCharacter(quest);
            if (characterId != null)
                ReevaluateCharacter(characterId, raise: true);
        }

        private string ResolveCharacter(IQuest quest)
        {
            if (quest == null) return null;
            if (quest.Id != null && _questToCharacter.TryGetValue(quest.Id, out var byQuest)) return byQuest;
            if (quest.ChainId != null && _chainToCharacter.TryGetValue(quest.ChainId, out var byChain)) return byChain;
            return null;
        }

        /// <summary>Seed (raise:false) on load, or react to a live quest event (raise:true).</summary>
        private void ReevaluateCharacter(string characterId, bool raise)
        {
            if (!_configsById.TryGetValue(characterId, out var config)) return;
            Discover(config, raise);
            UnlockMatchingMemories(config, raise);
        }

        private void Reconcile()
        {
            foreach (var config in _configsById.Values)
                ReevaluateCharacter(config.Id, raise: false);
        }

        private void Discover(CharacterConfig config, bool raise)
        {
            if (!_factory.IsDiscoveredByQuest(config)) return;

            var saved = GetOrCreateSaved(config.Id);
            if (saved.Discovered) return;

            saved.Discovered = true;
            SetDirty();
            if (raise) CharacterDiscovered?.Invoke(_factory.Create(config, saved));
        }

        private void UnlockMatchingMemories(CharacterConfig config, bool raise)
        {
            var memories = config.Memories;
            if (memories == null) return;

            for (var i = 0; i < memories.Length; i++)
            {
                var mc = memories[i];
                if (!_factory.IsUnlockedByQuest(mc)) continue;

                var saved = GetOrCreateSaved(config.Id);
                saved.UnlockedMemoryIds ??= new HashSet<string>(StringComparer.Ordinal);
                if (!saved.UnlockedMemoryIds.Add(mc.Id)) continue; // already announced → idempotent

                SetDirty();
                if (raise)
                    MemoryUnlocked?.Invoke(
                        new CharacterMemory(mc.Id, config.Id, true, mc.IsGolden, mc.QuestId, mc.QuestChainId));
            }
        }

        // ----- internals -----

        private void BuildCatalog()
        {
            _configsById.Clear();
            _questToCharacter.Clear();
            _chainToCharacter.Clear();

            foreach (var config in _configs.GetAll<CharacterConfig>())
            {
                if (config?.Id == null) continue;
                _configsById[config.Id] = config;
                IndexCharacter(config);
            }
        }

        private void IndexCharacter(CharacterConfig config)
        {
            Index(_questToCharacter, config.DiscoveryQuestIds, config.Id);
            Index(_chainToCharacter, config.DiscoveryQuestChainIds, config.Id);

            var memories = config.Memories;
            if (memories == null) return;
            for (var i = 0; i < memories.Length; i++)
            {
                var mc = memories[i];
                if (!string.IsNullOrEmpty(mc.QuestId)) _questToCharacter[mc.QuestId] = config.Id;
                if (!string.IsNullOrEmpty(mc.QuestChainId)) _chainToCharacter[mc.QuestChainId] = config.Id;
            }
        }

        private static void Index(Dictionary<string, string> map, string[] keys, string characterId)
        {
            if (keys == null) return;
            for (var i = 0; i < keys.Length; i++)
                if (!string.IsNullOrEmpty(keys[i])) map[keys[i]] = characterId;
        }

        private SavedCharacter GetOrCreateSaved(string characterId)
        {
            _saved ??= new SavedCharacters();
            _saved.Characters ??= new Dictionary<string, SavedCharacter>(StringComparer.Ordinal);
            if (!_saved.Characters.TryGetValue(characterId, out var saved))
            {
                saved = new SavedCharacter();
                _saved.Characters[characterId] = saved;
            }
            return saved;
        }

        private SavedCharacter GetSaved(string characterId)
            => _saved?.Characters != null && _saved.Characters.TryGetValue(characterId, out var s) ? s : null;

        private void SetDirty()
        {
            _dirty = true;
            _save.MarkDirty();
        }

        public void Dispose() => Unsubscribe();
    }
}
