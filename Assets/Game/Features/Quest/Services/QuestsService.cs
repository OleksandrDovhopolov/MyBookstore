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

        public QuestsService(
            ISaveService save,
            IConfigsService configs,
            IConditionParser parser,
            ISalesStatsService sales = null,
            IDecorPlacementService decor = null,
            IInventoryService inventory = null,
            IDayProgressService dayProgress = null)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
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

        public UniTask AfterLoadAsync(CancellationToken ct)
        {
            BuildCatalog();
            Subscribe();
            _loaded = true;
            Reevaluate(); // initial head activation
            Debug.Log($"{LogPrefix} loaded: {_quests.Count} quests.");
            return UniTask.CompletedTask;
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask; // Этап 4: in-memory only

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
                        changed = true;
                    }

                    if (task.State == QuestTaskState.Active)
                    {
                        if (task.RefreshProgress()) TaskProgressChanged?.Invoke(task);
                        if (task.IsCompletionMet)
                        {
                            task.SetState(QuestTaskState.Completed);
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
            QuestStarted?.Invoke(quest);
        }

        private void Complete(Quest quest)
        {
            quest.SetState(QuestState.ReadyToAward);
            QuestCompleted?.Invoke(quest);
        }

        private void Award(Quest quest)
        {
            quest.SetState(QuestState.Awarded);
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
            QuestFailed?.Invoke(quest);
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

        // ----- helpers -----

        private static bool HasValues(Newtonsoft.Json.Linq.JObject node) => node != null && node.HasValues;

        private static Quest FirstOrDefault(IEnumerable<Quest> quests)
        {
            foreach (var q in quests) return q;
            return null;
        }
    }
}
