using System.Collections.Generic;
using Game.Characters.UI;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.TutorialUI;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHub : ITutorialSequence
    {
        private const int JournalWindowTimeoutMs = 5000;

        private readonly IDayProgressService _dayProgress;
        private readonly IUIManager _ui;
        private readonly TutorialOverlayController _overlay;
        private readonly ITutorialTargetRegistry _targets;

        public TutorialHub(
            IDayProgressService dayProgress,
            IUIManager ui = null,
            TutorialOverlayController overlay = null,
            ITutorialTargetRegistry targets = null)
        {
            _dayProgress = dayProgress;
            _ui = ui;
            _overlay = overlay;
            _targets = targets;
        }

        public string Id => TutorialSequenceIds.Hub;
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
                   && !ResultsShown;
        }

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialDialogueStep("hub_dialogue", _ui, TutorialContent.Dialogues.HubIntro),
                new TutorialHighlightClickStep(
                    "click_journal_button",
                    _overlay,
                    _targets,
                    TutorialTargetIds.HubJournalButton,
                    TutorialTexts.JournalHighlight,
                    TutorialContent.Placements.Bottom,
                    pointer: true,
                    pointerPlacement: TutorialPointerPlacement.Top),
                new TutorialAwaitWindowStep(
                    "wait_journal_window",
                    () => _ui != null && _ui.IsWindowShown<JournalWindow>(),
                    JournalWindowTimeoutMs,
                    failOpen: true)
            };

        public void OnRunStarted() { }

        public void OnRunEnded() => _overlay?.Hide();

        private bool ResultsShown => _ui != null && _ui.IsWindowShown<ResultsWindow>();
    }
}
