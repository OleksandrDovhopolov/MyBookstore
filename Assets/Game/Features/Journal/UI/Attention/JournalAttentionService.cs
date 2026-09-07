using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Characters.API;
using Game.Configs;
using Game.Configs.Models;
using Game.LocationUnlock.API;
using Game.Quest.API;
using Save;

namespace Game.Journal.UI
{
    public sealed class JournalAttentionService : IJournalAttentionService, ISaveHook, IDisposable
    {
        private readonly ISaveService _save;
        private readonly IJournalAttentionRepository _repository;
        private readonly IQuestsService _quests;
        private readonly ILocationUnlockService _locations;
        private readonly ICharactersService _characters;
        private readonly IConfigsService _configs;
        private readonly Dictionary<JournalAttentionCategory, bool> _lastUnseen = new();

        private SavedJournalAttention _saved = new();
        private bool _dirty;
        private bool _loaded;
        private bool _subscribed;

        public JournalAttentionService(
            ISaveService save,
            IJournalAttentionRepository repository,
            IQuestsService quests,
            ILocationUnlockService locations,
            ICharactersService characters,
            IConfigsService configs)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));

            _save.RegisterHook(this);
        }

        public event Action Changed;

        public bool HasAnyUnseen =>
            HasUnseen(JournalAttentionCategory.Quests)
            || HasUnseen(JournalAttentionCategory.Places)
            || HasUnseen(JournalAttentionCategory.People)
            || HasUnseen(JournalAttentionCategory.Memories);

        public bool HasUnseen(JournalAttentionCategory category)
        {
            return category switch
            {
                JournalAttentionCategory.Quests => ContainsUnseen(CurrentQuestNewIds(), _saved.SeenQuestNewIds)
                                                   || ContainsUnseen(CurrentQuestAwardIds(), _saved.SeenQuestAwardIds),
                JournalAttentionCategory.Places => ContainsUnseen(CurrentPlaceIds(), _saved.SeenPlaceIds),
                JournalAttentionCategory.People => ContainsUnseen(CurrentPeopleIds(), _saved.SeenPeopleIds),
                JournalAttentionCategory.Memories => _characters.HasUnseenMemories,
                _ => false
            };
        }

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            _saved = await _repository.LoadAsync(ct) ?? new SavedJournalAttention();
            EnsureSets();
            _loaded = true;
            Subscribe();
            CaptureSnapshot();
        }

        public UniTask BeforeSaveAsync(CancellationToken ct)
        {
            if (!_dirty) return UniTask.CompletedTask;
            _dirty = false;
            return _repository.SaveAsync(_saved, ct);
        }

        public void MarkSeen(JournalAttentionCategory category)
        {
            EnsureSets();

            var changed = category switch
            {
                JournalAttentionCategory.Quests => AddAll(_saved.SeenQuestNewIds, CurrentQuestNewIds())
                                                   | AddAll(_saved.SeenQuestAwardIds, CurrentQuestAwardIds()),
                JournalAttentionCategory.Places => AddAll(_saved.SeenPlaceIds, CurrentPlaceIds()),
                JournalAttentionCategory.People => AddAll(_saved.SeenPeopleIds, CurrentPeopleIds()),
                JournalAttentionCategory.Memories => MarkMemoriesSeen(),
                _ => false
            };

            if (changed && category != JournalAttentionCategory.Memories)
                SetDirty();

            NotifyIfChanged();
        }

        public void Dispose()
        {
            Unsubscribe();
        }

        private bool MarkMemoriesSeen()
        {
            var hadUnseen = _characters.HasUnseenMemories;
            _characters.MarkAllMemoriesSeen();
            return hadUnseen != _characters.HasUnseenMemories;
        }

        private IEnumerable<string> CurrentQuestNewIds()
            => CurrentQuestIds(QuestState.Active);

        private IEnumerable<string> CurrentQuestAwardIds()
            => CurrentQuestIds(QuestState.ReadyToAward);

        private IEnumerable<string> CurrentQuestIds(QuestState state)
        {
            var quests = _quests.GetAllQuests();
            if (quests == null) yield break;

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                if (quest.State == state)
                    yield return quest.Id;
            }
        }

        private IEnumerable<string> CurrentPlaceIds()
        {
            var locations = _configs.GetAll<LocationConfig>();
            if (locations == null) yield break;

            for (var i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null || string.IsNullOrEmpty(location.Id)) continue;
                if (_locations.IsUnlocked(location.Id))
                    yield return location.Id;
            }
        }

        private IEnumerable<string> CurrentPeopleIds()
        {
            var characters = _characters.GetDiscoveredCharacters();
            if (characters == null) yield break;

            foreach (var character in characters)
            {
                if (character == null || string.IsNullOrEmpty(character.Id)) continue;
                yield return character.Id;
            }
        }

        private static bool ContainsUnseen(IEnumerable<string> current, HashSet<string> seen)
        {
            foreach (var id in current)
                if (!seen.Contains(id))
                    return true;
            return false;
        }

        private static bool AddAll(HashSet<string> target, IEnumerable<string> ids)
        {
            var changed = false;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id))
                    changed |= target.Add(id);
            return changed;
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            _quests.QuestStarted += OnQuestChanged;
            _quests.QuestCompleted += OnQuestChanged;
            _quests.QuestAwarded += OnQuestChanged;
            _quests.QuestFailed += OnQuestChanged;
            _locations.Unlocked += OnLocationChanged;
            _locations.StatusChanged += OnLocationChanged;
            _characters.CharacterDiscovered += OnCharacterChanged;
            _characters.MemoryUnlocked += OnMemoryChanged;
            _characters.UnseenMemoriesChanged += OnUnseenMemoriesChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            _quests.QuestStarted -= OnQuestChanged;
            _quests.QuestCompleted -= OnQuestChanged;
            _quests.QuestAwarded -= OnQuestChanged;
            _quests.QuestFailed -= OnQuestChanged;
            _locations.Unlocked -= OnLocationChanged;
            _locations.StatusChanged -= OnLocationChanged;
            _characters.CharacterDiscovered -= OnCharacterChanged;
            _characters.MemoryUnlocked -= OnMemoryChanged;
            _characters.UnseenMemoriesChanged -= OnUnseenMemoriesChanged;
            _subscribed = false;
        }

        private void OnQuestChanged(IQuest _) => NotifyIfChanged();
        private void OnLocationChanged(string _) => NotifyIfChanged();
        private void OnCharacterChanged(ICharacter _) => NotifyIfChanged();
        private void OnMemoryChanged(ICharacterMemory _) => NotifyIfChanged();
        private void OnUnseenMemoriesChanged() => NotifyIfChanged();

        private void NotifyIfChanged()
        {
            if (!_loaded) return;

            var changed = false;
            foreach (JournalAttentionCategory category in Enum.GetValues(typeof(JournalAttentionCategory)))
            {
                var value = HasUnseen(category);
                if (_lastUnseen.TryGetValue(category, out var previous) && previous == value)
                    continue;

                _lastUnseen[category] = value;
                changed = true;
            }

            if (changed)
                Changed?.Invoke();
        }

        private void CaptureSnapshot()
        {
            _lastUnseen.Clear();
            foreach (JournalAttentionCategory category in Enum.GetValues(typeof(JournalAttentionCategory)))
                _lastUnseen[category] = HasUnseen(category);
        }

        private void SetDirty()
        {
            _dirty = true;
            _save.MarkDirty();
        }

        private void EnsureSets()
        {
            _saved ??= new SavedJournalAttention();
            _saved.SeenQuestNewIds ??= new HashSet<string>(StringComparer.Ordinal);
            _saved.SeenQuestAwardIds ??= new HashSet<string>(StringComparer.Ordinal);
            _saved.SeenPlaceIds ??= new HashSet<string>(StringComparer.Ordinal);
            _saved.SeenPeopleIds ??= new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
