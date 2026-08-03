using System;
using Game.Configs;
using Game.Quest.API;
using Game.UI;
using SpriteService;
using VContainer;

namespace Game.Quest.UI
{
    /// <summary>
    /// In-game quest journal: shows non-pending quests and lets the player claim completed rewards.
    /// </summary>
    [Window("QuestWindow", WindowType.Page)]
    public sealed class QuestWindow : WindowController<QuestWindowView>
    {
        private QuestViewModelBuilder _builder;
        private QuestClaimFlow _claimFlow;
        private IQuestsService _quests;
        private IQuestRewardGranter _granter;
        private IConfigsService _configs;
        private IUiSpriteProvider _sprites;
        private Action<string> _onClaim;

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
            _claimFlow = new QuestClaimFlow(_quests, _granter, UIManager, Render);
            _onClaim = questId => _claimFlow?.Claim(questId);
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
            _claimFlow?.Dispose();
            _claimFlow = null;
            View.Clear();
        }

        private void OnQuestChanged(IQuest _) => Render();
        private void OnTaskChanged(IQuestTask _) => Render();

        private void Render()
        {
            if (_claimFlow is { SuppressRender: true }) return;

            if (_quests == null || _builder == null)
            {
                View.Clear();
                return;
            }

            View.Render(_builder.Build(_quests.GetAllQuests()), _onClaim, _sprites);
        }
    }
}
