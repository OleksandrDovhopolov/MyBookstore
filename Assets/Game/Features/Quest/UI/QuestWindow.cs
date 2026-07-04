using Game.Quest.API;
using Game.UI;
using VContainer;

namespace Game.Quest.UI
{
    /// <summary>
    /// Quest journal page: lists the player's active quests (title / description / primary-task progress /
    /// state) via a pooled row view, live-refreshing on quest and task events. Mirrors
    /// <c>Game.Characters.UI.JournalWindow</c>. Opened via <c>uiManager.ShowAsync&lt;QuestWindow&gt;()</c>;
    /// needs a prefab at address "QuestWindow".
    /// </summary>
    [Window("QuestWindow", WindowType.Page)]
    public sealed class QuestWindow : WindowController<QuestWindowView>
    {
        private readonly QuestViewModelBuilder _builder = new();

        private IQuestsService _quests;

        [Inject]
        public void InjectServices(IQuestsService quests)
        {
            _quests = quests;
        }

        protected override void OnInit()
        {
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

        protected override void OnDispose() => View.Clear();

        private void OnQuestChanged(IQuest _) => Render();
        private void OnTaskChanged(IQuestTask _) => Render();

        private void Render()
        {
            if (_quests == null)
            {
                View.Clear();
                return;
            }

            View.Render(_builder.Build(_quests.GetActiveQuests()));
        }
    }
}
