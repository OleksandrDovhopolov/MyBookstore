using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Localization;
using Game.Quest.API;
using Game.Rewards.UI;
using Game.UI;
using UnityEngine;

namespace Game.Quest.UI
{
    public sealed class QuestClaimFlow : IDisposable
    {
        private const string LogTag = "[QuestClaimFlow]";

        private readonly HashSet<string> _claiming = new(StringComparer.Ordinal);
        private readonly IQuestsService _quests;
        private readonly IQuestRewardGranter _granter;
        private readonly IUIManager _ui;
        private readonly Action _onChanged;

        private CancellationTokenSource _cts = new();

        public QuestClaimFlow(
            IQuestsService quests,
            IQuestRewardGranter granter,
            IUIManager ui,
            Action onChanged)
        {
            _quests = quests;
            _granter = granter;
            _ui = ui;
            _onChanged = onChanged;
        }

        public bool SuppressRender { get; private set; }

        public void Claim(string questId)
        {
            if (string.IsNullOrEmpty(questId) || _quests == null || _granter == null) return;
            if (!_claiming.Add(questId)) return;

            SuppressRender = true;
            ClaimAsync(questId).Forget();
        }

        private async UniTaskVoid ClaimAsync(string questId)
        {
            try
            {
                var token = _cts?.Token ?? CancellationToken.None;
                if (!await _quests.TryAwardAsync(questId, token))
                    return;

                var result = await _granter.TryGrantAsync(questId, token);
                if (!result.Success)
                {
                    Debug.LogError($"{LogTag} Failed to grant reward for quest '{questId}': {result.FailureReason}");
                    return;
                }

                SuppressRender = false;
                _onChanged?.Invoke();

                if (result.Granted?.Items != null && result.Granted.Items.Count > 0 && _ui != null)
                {
                    await _ui.ShowAsync<RewardsWindow>(
                        new RewardsWindowArgs(result.Granted, LocalizationLocator.GetOrKey("ui.quest.reward.title")),
                        token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} Claim failed for quest '{questId}': {e.Message}");
            }
            finally
            {
                _claiming.Remove(questId);
                SuppressRender = false;
                _onChanged?.Invoke();
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _claiming.Clear();
            SuppressRender = false;
        }
    }
}
