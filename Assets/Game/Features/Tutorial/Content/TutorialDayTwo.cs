using System.Collections.Generic;
using Game.DayCycle.Day;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDayTwo : ITutorialSequence
    {
        private const string PlaceholderText = "[TUTORIAL DAY 2 PLACEHOLDER]";
        private const string BottomPlacement = "bottom";

        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;
        private readonly IDayProgressService _dayProgress;

        public TutorialDayTwo(TutorialOverlayController overlay, IUIManager ui, IDayProgressService dayProgress)
        {
            _overlay = overlay;
            _ui = ui;
            _dayProgress = dayProgress;
        }

        public string Id => "tutorial_day_2";
        public int Priority => 20;
        public TutorialContext Context => TutorialContext.Location;
        public TutorialTrigger Trigger => TutorialTrigger.LocationLoaded;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible() => _dayProgress.Current.CurrentDay == 2;

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialShowTextStep("day_2_placeholder", _overlay, _ui, PlaceholderText, BottomPlacement),
            };
    }
}
