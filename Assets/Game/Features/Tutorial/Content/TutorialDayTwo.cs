using System;
using System.Collections.Generic;
using Analytics;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.TutorialUI;
using MessagePipe;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDayTwo : ITutorialSequence
    {
        private const string GenrePanelTargetId = "location.genre_book_count_panel";
        private const string Text1 = "The presence of books in the genres themselves does not guarantee sales.";
        private const string Text2 =
            "The more books you have on your shelves in a particular genre, the higher your chance of selling them.";
        private const string Text3 = "Click on a book to find out its chance of sale.";
        private const string HighlightText = "Tap a genre to inspect sale chance.";
        private const string Text4 =
            "This is the chance a book of that genre will sell. Stock more of a genre to raise it.";
        private const string BottomPlacement = "bottom";
        private const string LogPrefix = "[Tutorial]";
        private const string PassiveFailMissingEvent = "tutorial_day2_passive_fail_missing";

        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;
        private readonly IDayProgressService _dayProgress;
        private readonly ITutorialTargetRegistry _targets;
        private readonly IPublisher<SalesPauseRequested> _pausePublisher;
        private readonly ISubscriber<SalesPassivePurchaseFailed> _failSub;
        private readonly IAnalyticsService _analytics;
        private readonly List<IDisposable> _subscriptions = new();

        private bool _passiveFailed;

        public TutorialDayTwo(
            TutorialOverlayController overlay,
            IUIManager ui,
            IDayProgressService dayProgress,
            ITutorialTargetRegistry targets,
            IPublisher<SalesPauseRequested> pausePublisher,
            ISubscriber<SalesPassivePurchaseFailed> failSub,
            IAnalyticsService analytics = null)
        {
            _overlay = overlay;
            _ui = ui;
            _dayProgress = dayProgress;
            _targets = targets;
            _pausePublisher = pausePublisher;
            _failSub = failSub;
            _analytics = analytics;
        }

        public string Id => "tutorial_day_2";
        public int Priority => 20;
        public TutorialContext Context => TutorialContext.Location;
        public TutorialTrigger Trigger => TutorialTrigger.LocationLoaded;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible() => _dayProgress.Current.CurrentDay == 2;
        private bool ResultsShown => _ui.IsWindowShown<ResultsWindow>();

        public void OnRunStarted()
        {
            ResetLatch();
            DisposeSubscriptions();
            _subscriptions.Add(_failSub.Subscribe(OnSalesPassivePurchaseFailed));
        }

        public void OnRunEnded()
        {
            _pausePublisher.Publish(new SalesPauseRequested(false));
            DisposeSubscriptions();
            ResetLatch();
            _overlay.HideCallout();
        }

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialAwaitFactStep("await_passive_fail", Until(() => _passiveFailed)),
                new TutorialAssertStep("verify_passive_fail", () => _passiveFailed, ReportPassiveFailMissing),
                new TutorialBlockingCalloutStep(
                    "text_1",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _passiveFailed,
                    () => Text1,
                    BottomPlacement),
                new TutorialBlockingCalloutStep(
                    "text_2",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _passiveFailed,
                    () => Text2,
                    BottomPlacement),
                new TutorialBlockingCalloutStep(
                    "text_3",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _passiveFailed,
                    () => Text3,
                    BottomPlacement),
                new TutorialHighlightClickStep(
                    "click_genre_panel",
                    _overlay,
                    _targets,
                    GenrePanelTargetId,
                    HighlightText,
                    BottomPlacement,
                    true,
                    () => _passiveFailed,
                    TutorialPointerPlacement.Left,
                    _pausePublisher,
                    pauseSales: true),
                new TutorialBlockingCalloutStep(
                    "text_4",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _passiveFailed,
                    () => Text4,
                    BottomPlacement,
                    hideTextAfterTap: true),
            };

        private void OnSalesPassivePurchaseFailed(SalesPassivePurchaseFailed message)
        {
            _passiveFailed = true;
        }

        private Func<bool> Until(Func<bool> fact)
            => () => fact() || ResultsShown;

        private void ReportPassiveFailMissing()
        {
            Debug.LogError(
                $"{LogPrefix} day 2 reached results without a passive purchase failure. " +
                "Check day2_missed_sale and the day-2 customer setup.");

            _analytics?.TrackEvent(new AnalyticsEvent(PassiveFailMissingEvent));
        }

        private void ResetLatch()
        {
            _passiveFailed = false;
        }

        private void DisposeSubscriptions()
        {
            for (var i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }
    }
}
