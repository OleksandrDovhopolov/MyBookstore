using System;
using System.Collections.Generic;
using Game.DayCycle.Day;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDayTwo : ITutorialSequence
    {
        private const string PlaceholderText = "[TUTORIAL DAY 2 PLACEHOLDER]";
        private const string BottomPlacement = "bottom";

        private readonly TutorialOverlayController _overlay;
        private readonly IDayProgressService _dayProgress;

        public TutorialDayTwo(TutorialOverlayController overlay, IDayProgressService dayProgress)
        {
            _overlay = overlay;
            _dayProgress = dayProgress;
        }

        public string Id => "tutorial_day_2";
        public int Priority => 20;
        public TutorialContext Context => TutorialContext.Location;
        public TutorialTrigger Trigger => TutorialTrigger.LocationLoaded;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible() => _dayProgress.Current.CurrentDay == 2;

        public void OnRunEnded() => _overlay.HideCallout();

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialShowCalloutStep("day_2_placeholder_show", _overlay, PlaceholderText, BottomPlacement),
                new TutorialDelayStep("day_2_placeholder_wait", TimeSpan.FromSeconds(3)),
                new TutorialHideCalloutStep("day_2_placeholder_hide", _overlay),
            };
    }
}
