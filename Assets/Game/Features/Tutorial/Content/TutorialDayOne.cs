using System.Collections.Generic;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDayOne : ITutorialSequence
    {
        private const string WelcomeText =
            "Welcome to your bookshop! You're open for your first day - customers buy books on their own as they browse.";
        private const string SaleChanceText =
            "Each sale depends on the genre's sale chance. Keep the shelf stocked with genres your visitors like.";
        private const string WrapUpText =
            "Day complete - nice work! From tomorrow you'll stock the shelf and choose where to trade yourself.";
        private const string BottomPlacement = "bottom";

        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;
        private readonly IDayProgressService _dayProgress;

        public TutorialDayOne(TutorialOverlayController overlay, IUIManager ui, IDayProgressService dayProgress)
        {
            _overlay = overlay;
            _ui = ui;
            _dayProgress = dayProgress;
        }

        public string Id => "tutorial_day_1";
        public int Priority => 10;
        public TutorialContext Context => TutorialContext.Location;
        public TutorialTrigger Trigger => TutorialTrigger.LocationLoaded;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible() => _dayProgress.Current.CurrentDay == 1;

        public void OnRunEnded() { }

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialShowTextStep("welcome", _overlay, _ui, WelcomeText, BottomPlacement),
                new TutorialShowTextStep("sale_chance", _overlay, _ui, SaleChanceText, BottomPlacement),
                new TutorialAwaitWindowStep("wait_results_window", () => _ui.IsWindowShown<ResultsWindow>()),
                new TutorialShowTextStep("wrap_up", _overlay, _ui, WrapUpText, BottomPlacement),
            };
    }
}
