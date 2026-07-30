using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Quest.API;
using Game.Rewards.UI;
using Game.UI;
using SpriteService;
using UnityEngine;
using VContainer;

namespace Game.Quest.UI
{
    /// <summary>
    /// In-game quest journal: shows non-pending quests and lets the player claim completed rewards.
    /// </summary>
    [Window("QuestWindow", WindowType.Page)]
    public sealed class QuestWindow : WindowController<QuestWindowView>
    {
        private readonly HashSet<string> _claiming = new(StringComparer.Ordinal);

        private QuestViewModelBuilder _builder;
        private IQuestsService _quests;
        private IQuestRewardGranter _granter;
        private IConfigsService _configs;
        private IUiSpriteProvider _sprites;
        private CancellationTokenSource _cts;
        private Action<string> _onClaim;
        private bool _suppressRender;

        [Inject]
        public void InjectServices(
            IQuestsService quests,
            IQuestRewardGranter granter,
            IConfigsService configs,
            IUiSpriteProvider sprites = null)
        {
            _quests = quests;
            _granter = granter;
            _configs = configs;
            _sprites = sprites;
        }

        protected override void OnInit()
        {
            _builder = new QuestViewModelBuilder(_configs);
            _onClaim = questId => ClaimAsync(questId).Forget();
            _cts = new CancellationTokenSource();
        }

        protected override void OnShowStart()
        {
            if (_quests != null)
            {
                _quests.QuestStarted += OnQuestChanged;
                _quests.QuestCompleted += OnQuestChanged;
                _quests.QuestAwarded += OnQuestChanged;
                _quests.QuestFailed += OnQuestChanged;
                _quests.TaskCompleted += OnTaskChanged;
                _quests.TaskProgressChanged += OnTaskChanged;
            }

            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_quests != null)
            {
                _quests.QuestStarted -= OnQuestChanged;
                _quests.QuestCompleted -= OnQuestChanged;
                _quests.QuestAwarded -= OnQuestChanged;
                _quests.QuestFailed -= OnQuestChanged;
                _quests.TaskCompleted -= OnTaskChanged;
                _quests.TaskProgressChanged -= OnTaskChanged;
            }
        }

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            View.Clear();
        }

        private void OnQuestChanged(IQuest _) => Render();
        private void OnTaskChanged(IQuestTask _) => Render();

        private void Render()
        {
            if (_suppressRender) return;

            if (_quests == null || _builder == null)
            {
                View.Clear();
                return;
            }

            View.Render(_builder.Build(_quests.GetAllQuests()), _onClaim, _sprites);
        }

        private async UniTaskVoid ClaimAsync(string questId)
        {
            if (string.IsNullOrEmpty(questId) || _quests == null || _granter == null) return;
            if (!_claiming.Add(questId)) return;

            _suppressRender = true;
            try
            {
                var token = _cts?.Token ?? CancellationToken.None;
                if (!await _quests.TryAwardAsync(questId, token))
                    return;

                var result = await _granter.TryGrantAsync(questId, token);
                if (!result.Success)
                {
                    Debug.LogError($"[QuestWindow] Failed to grant reward for quest '{questId}': {result.FailureReason}");
                    return;
                }

                _suppressRender = false;
                Render();

                if (result.Granted?.Items != null && result.Granted.Items.Count > 0)
                {
                    await UIManager.ShowAsync<RewardsWindow>(
                        new RewardsWindowArgs(result.Granted, "Quest reward"),
                        token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _claiming.Remove(questId);
                _suppressRender = false;
                Render();
            }
        }
    }
}
