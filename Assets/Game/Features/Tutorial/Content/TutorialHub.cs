using System.Collections.Generic;
using Game.DayCycle.Day;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHub : ITutorialSequence
    {
        private readonly IDayProgressService _dayProgress;

        public TutorialHub(IDayProgressService dayProgress)
        {
            _dayProgress = dayProgress;
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
                   && state.CurrentPhase == DayPhase.Morning;
        }

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialLogStep("hub_log", "[Tutorial] TutorialHub reached (stub).")
            };

        public void OnRunStarted() { }
        public void OnRunEnded() { }
    }
}
