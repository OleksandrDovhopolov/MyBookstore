using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Quest.API;
using Game.Rewards.API;
using Save;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Grants authored quest rewards once a quest reaches Awarded, using a save-backed ledger so stack
    /// rewards are not granted again on later saves or reloads.
    /// </summary>
    public sealed class QuestRewardBridge : ISaveHook
    {
        private const string LogPrefix = "[QuestRewards]";
        private const string ModuleKey = "quest_rewards.granted";
        private const int SchemaVersion = 1;

        private readonly ISaveService _save;
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;
        private readonly IRewardGrantService _rewards;
        private readonly HashSet<string> _granted = new(StringComparer.Ordinal);

        public QuestRewardBridge(
            ISaveService save,
            IConfigsService configs,
            IQuestsService quests,
            IRewardGrantService rewards)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));

            _save.RegisterHook(this);
        }

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            _granted.Clear();

            var state = await _save.GetModuleAsync<QuestRewardBridgeState>(ModuleKey, ct);
            if (state?.GrantedQuestIds == null) return;

            for (var i = 0; i < state.GrantedQuestIds.Count; i++)
            {
                var questId = state.GrantedQuestIds[i];
                if (!string.IsNullOrEmpty(questId))
                    _granted.Add(questId);
            }
        }

        public async UniTask BeforeSaveAsync(CancellationToken ct)
        {
            var changed = false;
            foreach (var quest in _configs.GetAll<QuestConfig>())
            {
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                if (_granted.Contains(quest.Id)) continue;
                if (_quests.GetQuestState(quest.Id) != QuestState.Awarded) continue;

                if (!TryBuildSpec(quest, out var spec)) continue;
                if (spec.Items.Count == 0)
                {
                    _granted.Add(quest.Id);
                    changed = true;
                    continue;
                }

                var result = await _rewards.GrantAsync(spec, spec.Id, ct);
                if (!result.Success)
                {
                    Debug.LogError($"{LogPrefix} grant failed for quest '{quest.Id}': {result.FailureReason}");
                    continue;
                }

                _granted.Add(quest.Id);
                changed = true;
            }

            if (changed)
                await SaveLedgerAsync(ct);
        }

        private bool TryBuildSpec(QuestConfig quest, out RewardSpec spec)
        {
            spec = null;
            var rewards = quest.Rewards;
            if (rewards == null || rewards.Length == 0)
            {
                spec = new RewardSpec(Source(quest.Id), Array.Empty<RewardItem>());
                return true;
            }

            var items = new List<RewardItem>(rewards.Length);
            for (var i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                if (reward == null) continue;
                if (string.IsNullOrEmpty(reward.Id) || reward.Amount <= 0)
                {
                    Debug.LogError($"{LogPrefix} quest '{quest.Id}' has invalid reward id/amount.");
                    continue;
                }

                if (string.Equals(reward.Kind, nameof(RewardKind.Resource), StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(RewardItem.Resource(reward.Id, reward.Amount));
                    continue;
                }

                if (string.Equals(reward.Kind, nameof(RewardKind.InventoryItem), StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(reward.Category))
                    {
                        Debug.LogError($"{LogPrefix} quest '{quest.Id}' inventory reward '{reward.Id}' has empty category.");
                        continue;
                    }

                    items.Add(RewardItem.InventoryItem(reward.Id, reward.Category, reward.Amount));
                    continue;
                }

                Debug.LogError($"{LogPrefix} quest '{quest.Id}' has unknown reward kind '{reward.Kind}'.");
            }

            spec = new RewardSpec(Source(quest.Id), items);
            return true;
        }

        private UniTask SaveLedgerAsync(CancellationToken ct)
        {
            var ids = new List<string>(_granted);
            ids.Sort(StringComparer.Ordinal);
            return _save.UpdateModuleAsync(ModuleKey, new QuestRewardBridgeState { GrantedQuestIds = ids }, SchemaVersion, ct);
        }

        private static string Source(string questId) => $"quest:{questId}";

        public sealed class QuestRewardBridgeState
        {
            public List<string> GrantedQuestIds { get; set; } = new();
        }
    }
}
