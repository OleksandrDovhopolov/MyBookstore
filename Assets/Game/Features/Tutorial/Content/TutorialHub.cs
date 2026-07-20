using System.Collections.Generic;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.UI;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHub : ITutorialSequence
    {
        private readonly IDayProgressService _dayProgress;
        private readonly IUIManager _ui;

        public TutorialHub(IDayProgressService dayProgress, IUIManager ui)
        {
            _dayProgress = dayProgress;
            _ui = ui;
        }

        public string Id => "tutorial_hub";
        public int Priority => 30;
        public TutorialContext Context => TutorialContext.Hub;
        public TutorialTrigger Trigger => TutorialTrigger.HubReady;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible()
        {
            var state = _dayProgress?.Current;
            return state?.CompletedDays != null
                   && state.CompletedDays.Contains(1)
                   && state.CurrentPhase == DayPhase.Morning
                   && !ResultsVisible;
        }

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialLogStep("hub_log", "[Tutorial] TutorialHub reached (stub).")
            };

        public void OnRunStarted() { }
        public void OnRunEnded() { }

        private bool ResultsVisible
            => _ui != null && (_ui.IsWindowShown<ResultsWindow>() || _ui.IsWindowSpawned<ResultsWindow>());
    }
}
