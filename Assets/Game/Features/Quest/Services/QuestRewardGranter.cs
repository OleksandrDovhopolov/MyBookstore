using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Quest.API;
using Game.Rewards.API;
using Save;
using UnityEngine;

namespace Game.Quest.Services
{
    public sealed class QuestRewardGranter : IQuestRewardGranter, ISaveHook
    {
        private const string LogPrefix = "[QuestRewards]";
        private const string ModuleKey = "quest_rewards.granted";
        private const int SchemaVersion = 1;

        private readonly ISaveService _save;
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;
        private readonly IRewardGrantService _rewards;
        private readonly IInventoryService _inventory;
        private readonly HashSet<string> _granted = new(StringComparer.Ordinal);
        private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);

        public QuestRewardGranter(
            ISaveService save,
            IConfigsService configs,
            IQuestsService quests,
            IRewardGrantService rewards,
            IInventoryService inventory)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            _save.RegisterHook(this);
        }

        public bool IsGranted(string questId)
            => !string.IsNullOrEmpty(questId) && _granted.Contains(questId);

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            _granted.Clear();

            var state = await _save.GetModuleAsync<QuestRewardGranterState>(ModuleKey, ct);
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
            foreach (var quest in _configs.GetAll<QuestConfig>())
            {
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                if (_quests.GetQuestState(quest.Id) != QuestState.Awarded) continue;

                var result = await TryGrantAsync(quest.Id, ct);
                if (!result.Success)
                    Debug.LogError($"{LogPrefix} sweep failed for quest '{quest.Id}': {result.FailureReason}");
            }
        }

        public async UniTask<QuestRewardGrantResult> TryGrantAsync(string questId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(questId))
                return QuestRewardGrantResult.Fail("empty_quest_id");

            if (_granted.Contains(questId))
                return QuestRewardGrantResult.Duplicate();

            if (!_inFlight.Add(questId))
                return QuestRewardGrantResult.Fail("in_flight");

            try
            {
                if (_quests.GetQuestState(questId) != QuestState.Awarded)
                    return QuestRewardGrantResult.Fail("not_awarded");

                var quest = _configs.Get<QuestConfig>(questId);
                if (quest == null)
                    return QuestRewardGrantResult.Fail("missing_config");

                if (!TryBuildSpec(quest, out var spec))
                    return QuestRewardGrantResult.Fail("invalid_spec");

                var granted = spec;

                if (spec.Items.Count > 0)
                {
                    var result = await _rewards.GrantAsync(spec, Source(questId), ct);
                    if (!result.Success)
                        return QuestRewardGrantResult.Fail(result.FailureReason);

                    granted = result.Granted;
                }

                await ConsumeCostsAsync(quest, ct);

                _granted.Add(questId);
                await SaveLedgerAsync(ct);
                return QuestRewardGrantResult.Ok(granted);
            }
            finally
            {
                _inFlight.Remove(questId);
            }
        }

        private async UniTask ConsumeCostsAsync(QuestConfig quest, CancellationToken ct)
        {
            var costs = quest.Costs;
            if (costs == null || costs.Length == 0) return;

            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost == null) continue;
                if (string.IsNullOrEmpty(cost.ItemId) || cost.Amount <= 0)
                {
                    Debug.LogError($"{LogPrefix} quest '{quest.Id}' has invalid cost id/amount.");
                    continue;
                }

                if (!await _inventory.RemoveAsync(cost.ItemId, cost.Amount, ct))
                {
                    Debug.LogError(
                        $"{LogPrefix} failed to consume cost '{cost.ItemId}' x{cost.Amount} for quest '{quest.Id}'.");
                }
            }
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
            return _save.UpdateModuleAsync(ModuleKey, new QuestRewardGranterState { GrantedQuestIds = ids }, SchemaVersion, ct);
        }

        private static string Source(string questId) => $"quest:{questId}";

        public sealed class QuestRewardGranterState
        {
            public List<string> GrantedQuestIds { get; set; } = new();
        }
    }
}
