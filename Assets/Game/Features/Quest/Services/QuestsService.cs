using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Decor;
using Game.Inventory.API;
using Game.Quest.API;
using Game.Quest.Services.Persistence;
using Game.SalesStats.API;
using Save;
using UnityEngine;

namespace Game.Quest.Services
{
    /// <summary>
    /// In-memory <see cref="IQuestsService"/> built on the Conditions engine (docs/QUESTS.md §11). Mirrors
    /// <c>LocationUnlockService</c>: registers as <see cref="ISaveHook"/> only for init timing — the catalog
    /// is built in <see cref="AfterLoadAsync"/> (configs are warm by then) and re-evaluated when domain data
    /// changes. Этап 4: no persistence (<see cref="BeforeSaveAsync"/> is a strict no-op); quest state is
    /// rebuilt from config each launch. Auto-award: completing all tasks goes ReadyToAward → Awarded at once.
    /// </summary>
    public sealed class QuestsService : IQuestsService, ISaveHook, IDisposable
    {
        private const string LogPrefix = "[Quests]";

        private readonly ISaveService _save;
        private readonly IQuestsRepository _repository;   // null → in-memory only (lifecycle tests)
        private readonly IConfigsService _configs;
        private readonly IConditionParser _parser;

        // Change sources (all optional, like LocationUnlock's salesStats). Re-evaluation is pull-based.
        private readonly ISalesStatsService _sales;
        private readonly IDecorPlacementService _decor;
        private readonly IInventoryService _inventory;
        private readonly IDayProgressService _dayProgress;

        private readonly Dictionary<string, Quest> _quests = new(StringComparer.Ordinal);
        private readonly HashSet<string> _successors = new(StringComparer.Ordinal);

        private bool _loaded;
        private bool _subscribed;
        private bool _reevaluating;
        private bool _reevalQueued;
        private bool _dirty;

        public QuestsService(
            ISaveService save,
            IConfigsService configs,
            IConditionParser parser,
            IQuestsRepository repository = null,
            ISalesStatsService sales = null,
            IDecorPlacementService decor = null,
            IInventoryService inventory = null,
            IDayProgressService dayProgress = null)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _repository = repository;
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _sales = sales;
            _decor = decor;
            _inventory = inventory;
            _dayProgress = dayProgress;

            save.RegisterHook(this);
        }

        public event Action<IQuest> QuestStarted;
        public event Action<IQuest> QuestCompleted;
        public event Action<IQuest> QuestAwarded;
        public event Action<IQuest> QuestFailed;
        public event Action<IQuestTask> TaskCompleted;
        public event Action<IQuestTask> TaskProgressChanged;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            BuildCatalog();
            if (_repository != null)
                ApplySaved(await _repository.LoadAsync(ct));
            Subscribe();
            _loaded = true;
            Reevaluate(); // initial head activation + offline progression
            Debug.Log($"{LogPrefix} loaded: {_quests.Count} quests.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct)
        {
            if (_repository == null || !_dirty) return UniTask.CompletedTask;
            _dirty = false;
            return _repository.SaveAsync(BuildDto(), ct);
        }

        // ----- IQuestsService (read) -----

        public IQuest TryGetQuest(string questId) => GetQuestInternal(questId);

        private Quest GetQuestInternal(string questId)
            => questId != null && _quests.TryGetValue(questId, out var q) ? q : null;

        public QuestConfig GetQuestConfig(string questId)
            => TryGetQuest(questId)?.Config;

        public QuestState GetQuestState(string questId)
            => TryGetQuest(questId)?.State ?? QuestState.Pending;

        public IEnumerable<IQuest> GetActiveQuests()
        {
            foreach (var q in _quests.Values)
                if (q.State == QuestState.Active) yield return q;
        }

        public IQuestChain GetChain(string chainId)
        {
            if (string.IsNullOrEmpty(chainId)) return null;

            var members = new Dictionary<string, Quest>(StringComparer.Ordinal);
            foreach (var q in _quests.Values)
                if (q.ChainId == chainId) members[q.Id] = q;
            if (members.Count == 0) return null;

            // Head = member not referenced as a NextQuestId by another member.
            var pointedTo = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in members.Values)
                foreach (var next in q.NextQuestIds)
                    if (members.ContainsKey(next)) pointedTo.Add(next);

            Quest head = null;
            foreach (var q in members.Values)
                if (!pointedTo.Contains(q.Id)) { head = q; break; }
            if (head == null) head = FirstOrDefault(members.Values); // cycle fallback

            var ordered = new List<IQuest>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var cursor = head;
            while (cursor != null && visited.Add(cursor.Id))
            {
                ordered.Add(cursor);
                cursor = cursor.NextQuestIds.Count > 0 && members.TryGetValue(cursor.NextQuestIds[0], out var n) ? n : null;
            }
            // Append any members not reachable from head (malformed chain) so nothing is silently dropped.
            foreach (var q in members.Values)
                if (!visited.Contains(q.Id)) ordered.Add(q);

            return new QuestChain(chainId, ordered);
        }

        public IQuestChain GetChainByQuestId(string questId)
        {
            var quest = TryGetQuest(questId);
            return quest == null ? null : GetChain(quest.ChainId);
        }

        // ----- IQuestsService (explicit transitions, idempotent) -----

        public UniTask<bool> TryActivateAsync(string questId, CancellationToken ct)
        {
            var quest = GetQuestInternal(questId);
            if (quest == null || quest.State != QuestState.Pending) return UniTask.FromResult(false);
            Activate(quest);
            Reevaluate();
            return UniTask.FromResult(true);
        }

        public UniTask<bool> TryAwardAsync(string questId, CancellationToken ct)
        {
            var quest = GetQuestInternal(questId);
            if (quest == null || quest.State != QuestState.ReadyToAward) return UniTask.FromResult(false);
            Award(quest);
            Reevaluate();
            return UniTask.FromResult(true);
        }

        public UniTask<bool> TryFailAsync(string questId, CancellationToken ct)
        {
            var quest = GetQuestInternal(questId);
            if (quest == null || quest.State != QuestState.Active) return UniTask.FromResult(false);
            FailQuest(quest);
            Reevaluate();
            return UniTask.FromResult(true);
        }

        // ----- catalog build + validation (§G) -----

        private void BuildCatalog()
        {
            _quests.Clear();
            _successors.Clear();

            foreach (var config in _configs.GetAll<QuestConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id))
                {
                    Debug.LogError($"{LogPrefix} skipping quest with empty Id.");
                    continue;
                }
                if (_quests.ContainsKey(config.Id))
                {
                    Debug.LogError($"{LogPrefix} duplicate quest Id '{config.Id}' — skipped.");
                    continue;
                }
                if (config.Tasks == null || config.Tasks.Length == 0)
                {
                    Debug.LogError($"{LogPrefix} quest '{config.Id}' has no tasks — skipped.");
                    continue;
                }

                if (!TryBuildTasks(config, out var tasks)) continue;

                if (!QuestTypeExtensions.TryParse(config.Type, out var type))
                {
                    Debug.LogError($"{LogPrefix} quest '{config.Id}' has unknown type '{config.Type}', defaulting to Story.");
                    type = QuestType.Story;
                }

                var activation = _parser.Parse(config.ActivationConditions);
                var fail = HasValues(config.FailConditions) ? _parser.Parse(config.FailConditions) : null;
                var next = NormalizeNext(config);

                _quests[config.Id] = new Quest(config, type, activation, fail, tasks, next);
            }

            // Successor set (only links to known quests) + validation.
            foreach (var quest in _quests.Values)
                foreach (var nextId in quest.NextQuestIds)
                {
                    if (!_quests.ContainsKey(nextId))
                        Debug.LogError($"{LogPrefix} quest '{quest.Id}' points to unknown successor '{nextId}'.");
                    else
                        _successors.Add(nextId);
                }

            ValidateChains();
        }

        private bool TryBuildTasks(QuestConfig config, out List<QuestTask> tasks)
        {
            tasks = new List<QuestTask>(config.Tasks.Length);
            var seen = new HashSet<int>();
            foreach (var taskCfg in config.Tasks)
            {
                if (taskCfg == null) continue;
                if (!seen.Add(taskCfg.Id))
                {
                    Debug.LogError($"{LogPrefix} quest '{config.Id}' has duplicate task Id {taskCfg.Id} — quest skipped.");
                    tasks = null;
                    return false;
                }
                var activation = _parser.Parse(taskCfg.ActivationConditions);
                var completion = _parser.Parse(taskCfg.CompletionConditions);
                tasks.Add(new QuestTask(config.Id, taskCfg, activation, completion));
            }
            return tasks.Count > 0;
        }

        private string[] NormalizeNext(QuestConfig config)
        {
            var next = config.NextQuestIds;
            if (next == null || next.Length == 0) return Array.Empty<string>();
            if (next.Length > 1)
                Debug.LogError($"{LogPrefix} quest '{config.Id}' has {next.Length} NextQuestIds; MVP chains are linear, using the first ('{next[0]}').");
            return new[] { next[0] };
        }

        private void ValidateChains()
        {
            // Multiple heads per chain + cycle detection (best-effort logging; build still proceeds).
            var headsPerChain = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var quest in _quests.Values)
            {
                if (string.IsNullOrEmpty(quest.ChainId)) continue;
                if (!_successors.Contains(quest.Id))
                    headsPerChain[quest.ChainId] = headsPerChain.TryGetValue(quest.ChainId, out var c) ? c + 1 : 1;
            }
            foreach (var pair in headsPerChain)
                if (pair.Value > 1)
                    Debug.LogError($"{LogPrefix} chain '{pair.Key}' has {pair.Value} heads — expected 1.");

            foreach (var quest in _quests.Values)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var cursor = quest;
                while (cursor != null && cursor.NextQuestIds.Count > 0)
                {
                    if (!visited.Add(cursor.Id))
                    {
                        Debug.LogError($"{LogPrefix} cycle detected in chain starting at '{quest.Id}'.");
                        break;
                    }
                    cursor = _quests.TryGetValue(cursor.NextQuestIds[0], out var n) ? n : null;
                }
            }
        }

        // ----- re-evaluation -----

        private void Reevaluate()
        {
            if (!_loaded) return;
            if (_reevaluating) { _reevalQueued = true; return; }

            _reevaluating = true;
            try
            {
                do
                {
                    _reevalQueued = false;
                    var changed = SinglePass();
                    if (!changed && !_reevalQueued) break;
                } while (true);
            }
            finally
            {
                _reevaluating = false;
            }
        }

        private bool SinglePass()
        {
            var changed = false;
            var snapshot = new List<Quest>(_quests.Values);

            // 1. Auto-activate eligible Pending heads.
            foreach (var quest in snapshot)
            {
                if (quest.State != QuestState.Pending) continue;
                if (_successors.Contains(quest.Id)) continue;       // chain successors wait for their predecessor
                if (!quest.IsActivationMet()) continue;
                Activate(quest);
                changed = true;
            }

            // 2. Active quests: fail first, then tasks/progress/completion/award.
            foreach (var quest in snapshot)
            {
                if (quest.State != QuestState.Active) continue;

                if (quest.IsFailMet())
                {
                    FailQuest(quest);
                    changed = true;
                    continue;
                }

                foreach (var task in quest.TasksInternal)
                {
                    if (task.State == QuestTaskState.Pending && task.IsActivationMet())
                    {
                        task.SetState(QuestTaskState.Active);
                        MarkDirty();
                        changed = true;
                    }

                    if (task.State == QuestTaskState.Active)
                    {
                        if (task.RefreshProgress()) TaskProgressChanged?.Invoke(task);
                        if (task.IsCompletionMet)
                        {
                            task.SetState(QuestTaskState.Completed);
                            MarkDirty();
                            TaskCompleted?.Invoke(task);
                            changed = true;
                        }
                    }
                }

                if (AllTasksCompleted(quest))
                {
                    Complete(quest);
                    Award(quest); // auto-award (MVP)
                    changed = true;
                }
            }

            return changed;
        }

        private static bool AllTasksCompleted(Quest quest)
        {
            var tasks = quest.TasksInternal;
            for (var i = 0; i < tasks.Count; i++)
                if (tasks[i].State != QuestTaskState.Completed) return false;
            return tasks.Count > 0;
        }

        // ----- transitions -----

        private void Activate(Quest quest)
        {
            quest.SetState(QuestState.Active);
            foreach (var task in quest.TasksInternal)
                task.SetState(task.IsActivationMet() ? QuestTaskState.Active : QuestTaskState.Pending);
            MarkDirty();
            QuestStarted?.Invoke(quest);
        }

        private void Complete(Quest quest)
        {
            quest.SetState(QuestState.ReadyToAward);
            MarkDirty();
            QuestCompleted?.Invoke(quest);
        }

        private void Award(Quest quest)
        {
            quest.SetState(QuestState.Awarded);
            MarkDirty();
            // Reward grant + permanent effects are Этап 6 — here we only signal.
            QuestAwarded?.Invoke(quest);
            ForceActivateFromChain(quest);
        }

        private void ForceActivateFromChain(Quest quest)
        {
            foreach (var nextId in quest.NextQuestIds)
            {
                if (!_quests.TryGetValue(nextId, out var next))
                {
                    Debug.LogError($"{LogPrefix} cannot start unknown successor '{nextId}' of '{quest.Id}'.");
                    continue;
                }
                if (next.State == QuestState.Pending)
                    Activate(next); // hard transition: chain link ignores successor activation conditions
            }
        }

        private void FailQuest(Quest quest)
        {
            quest.SetState(QuestState.Failed);
            foreach (var task in quest.TasksInternal)
                if (!task.State.IsClosed()) task.SetState(QuestTaskState.Failed);
            MarkDirty();
            QuestFailed?.Invoke(quest);
        }

        private void MarkDirty()
        {
            _dirty = true;
            _save.MarkDirty();
        }

        // ----- change subscriptions -----

        private void Subscribe()
        {
            Unsubscribe();
            if (_sales != null) _sales.Changed += OnSalesChanged;
            if (_decor != null) _decor.PlacementChanged += OnPlacementChanged;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
            if (_dayProgress != null) _dayProgress.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_sales != null) _sales.Changed -= OnSalesChanged;
            if (_decor != null) _decor.PlacementChanged -= OnPlacementChanged;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            if (_dayProgress != null) _dayProgress.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
        }

        private void OnSalesChanged(SalesStatsChange _) => Reevaluate();
        private void OnPlacementChanged() => Reevaluate();
        private void OnInventoryChanged(InventoryChangeEvent _) => Reevaluate();
        private void OnPhaseChanged(DayProgressState _) => Reevaluate();

        public void Dispose() => Unsubscribe();

        // ----- persistence (load/merge + build) -----

        /// <summary>Restores saved state onto the freshly built catalog. Silent: no events fire (terminal
        /// states must not re-grant; restored Active/RTA progression is replayed by the later Reevaluate).</summary>
        private void ApplySaved(SavedQuests dto)
        {
            if (dto == null) return;

            if (dto.Failed != null)
                foreach (var id in dto.Failed)
                    if (_quests.TryGetValue(id, out var q)) RestoreTerminal(q, QuestState.Failed, QuestTaskState.Failed);

            if (dto.Awarded != null)
                foreach (var id in dto.Awarded)
                    if (_quests.TryGetValue(id, out var q)) RestoreTerminal(q, QuestState.Awarded, QuestTaskState.Completed);

            if (dto.Active != null)
                foreach (var pair in dto.Active)
                    if (_quests.TryGetValue(pair.Key, out var q)) RestoreActive(q, pair.Value);

            // Safety: a partial save could leave an awarded quest's successor Pending — relink silently.
            foreach (var quest in _quests.Values)
            {
                if (quest.State != QuestState.Awarded) continue;
                foreach (var nextId in quest.NextQuestIds)
                    if (_quests.TryGetValue(nextId, out var succ) && succ.State == QuestState.Pending)
                        ActivateSilent(succ);
            }
        }

        private static void RestoreTerminal(Quest quest, QuestState state, QuestTaskState taskState)
        {
            quest.SetState(state);
            foreach (var task in quest.TasksInternal) task.SetState(taskState);
        }

        private static void RestoreActive(Quest quest, SavedQuest saved)
        {
            if (saved == null) return;
            quest.SetState(saved.State);
            if (saved.Tasks == null) return;
            foreach (var task in quest.TasksInternal)
                if (saved.Tasks.TryGetValue(task.Id, out var taskState))
                {
                    task.SetState(taskState);
                    if (taskState == QuestTaskState.Active) task.RefreshProgress();
                }
        }

        private static void ActivateSilent(Quest quest)
        {
            quest.SetState(QuestState.Active);
            foreach (var task in quest.TasksInternal)
                task.SetState(task.IsActivationMet() ? QuestTaskState.Active : QuestTaskState.Pending);
        }

        private SavedQuests BuildDto()
        {
            var dto = new SavedQuests
            {
                Active = new Dictionary<string, SavedQuest>(StringComparer.Ordinal),
                Awarded = new List<string>(),
                Failed = new List<string>()
            };

            foreach (var quest in _quests.Values)
            {
                switch (quest.State)
                {
                    case QuestState.Awarded:
                        dto.Awarded.Add(quest.Id);
                        break;
                    case QuestState.Failed:
                        dto.Failed.Add(quest.Id);
                        break;
                    case QuestState.Active:
                    case QuestState.ReadyToAward:
                        var tasks = new Dictionary<int, QuestTaskState>();
                        foreach (var task in quest.TasksInternal) tasks[task.Id] = task.State;
                        dto.Active[quest.Id] = new SavedQuest { State = quest.State, Tasks = tasks };
                        break;
                    // Pending: not persisted.
                }
            }

            return dto;
        }

        // ----- helpers -----

        private static bool HasValues(Newtonsoft.Json.Linq.JObject node) => node != null && node.HasValues;

        private static Quest FirstOrDefault(IEnumerable<Quest> quests)
        {
            foreach (var q in quests) return q;
            return null;
        }
    }
}
