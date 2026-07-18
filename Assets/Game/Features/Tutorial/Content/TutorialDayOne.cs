using System;
using System.Collections.Generic;
using Analytics;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using MessagePipe;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDayOne : ITutorialSequence
    {
        private const string EddiCharacterId = "eddi";
        private const string BrowsingPhase = "Browsing";
        private const string DonePhase = "Done";
        private const string EddiSearchText =
            "Eddi is looking for a book. Watch how the shelf and sale chance work together.";
        private const string EddiSoldText =
            "Nice - Eddi bought a {0} book. A good genre match makes passive sales much more likely.";
        private const string EddiFailedText =
            "That {0} attempt missed. Even interested customers can walk away when the sale chance does not roll your way.";
        private const string WrapUpText =
            "Day complete - nice work! From tomorrow you'll stock the shelf and choose where to trade yourself.";
        private const string BottomPlacement = "bottom";
        private const string LogPrefix = "[Tutorial]";
        private const string EddiIncompleteEvent = "tutorial_day1_eddi_incomplete";

        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;
        private readonly IDayProgressService _dayProgress;
        private readonly ISubscriber<SalesCustomerPhaseChanged> _phaseSub;
        private readonly ISubscriber<SalesPassiveSaleHappened> _saleSub;
        private readonly ISubscriber<SalesPassivePurchaseFailed> _failSub;
        private readonly IPublisher<SalesPauseRequested> _pausePublisher;
        private readonly IAnalyticsService _analytics;
        private readonly List<IDisposable> _subscriptions = new();
        private bool _eddiBrowsing;
        private bool _eddiSold;
        private bool _eddiFailed;
        private bool _eddiLeft;
        private string _eddiSoldGenre;
        private string _eddiFailedGenre;

        public TutorialDayOne(
            TutorialOverlayController overlay,
            IUIManager ui,
            IDayProgressService dayProgress,
            ISubscriber<SalesCustomerPhaseChanged> phaseSub,
            ISubscriber<SalesPassiveSaleHappened> saleSub,
            ISubscriber<SalesPassivePurchaseFailed> failSub,
            IPublisher<SalesPauseRequested> pausePublisher,
            IAnalyticsService analytics)
        {
            _overlay = overlay;
            _ui = ui;
            _dayProgress = dayProgress;
            _phaseSub = phaseSub;
            _saleSub = saleSub;
            _failSub = failSub;
            _pausePublisher = pausePublisher;
            _analytics = analytics;
        }

        public string Id => "tutorial_day_1";
        public int Priority => 10;
        public TutorialContext Context => TutorialContext.Location;
        public TutorialTrigger Trigger => TutorialTrigger.LocationLoaded;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible() => _dayProgress.Current.CurrentDay == 1;
        private bool ResultsShown => _ui.IsWindowShown<ResultsWindow>();

        public void OnRunStarted()
        {
            ResetLatch();
            DisposeSubscriptions();
            _subscriptions.Add(_phaseSub.Subscribe(OnSalesCustomerPhaseChanged));
            _subscriptions.Add(_saleSub.Subscribe(OnSalesPassiveSaleHappened));
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
                new TutorialAwaitFactStep("await_eddi_dialogue_complete", Until(() => _eddiBrowsing)),
                new TutorialBlockingCalloutStep(
                    "callout_search",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _eddiBrowsing,
                    () => EddiSearchText,
                    BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_sale", Until(() => _eddiSold)),
                new TutorialShowCalloutStep(
                    "callout_sold",
                    _overlay,
                    () => FormatGenreCallout(_eddiSold, _eddiSoldGenre, EddiSoldText),
                    BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_fail", Until(() => _eddiFailed)),
                new TutorialShowCalloutStep(
                    "callout_failed",
                    _overlay,
                    () => FormatGenreCallout(_eddiFailed, _eddiFailedGenre, EddiFailedText),
                    BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_left", Until(() => _eddiLeft)),
                new TutorialHideCalloutStep("hide_eddi_callout", _overlay),
                // Eddi's scripted beats are the whole point of day 1. If the guaranteed sale never landed
                // (Eddi absent, or Fact missing from the shelf — a content desync, see TODO GAME-17), the
                // day still completes cleanly, but the lesson silently did not happen. Report it.
                new TutorialAssertStep("verify_eddi_participated", () => _eddiSold, ReportEddiIncomplete),
                new TutorialAwaitWindowStep("wait_results_window", () => ResultsShown),
                new TutorialShowCalloutStep("wrap_up", _overlay, WrapUpText, BottomPlacement),
                new TutorialAwaitWindowStep("wait_results_closed", () => !ResultsShown),
            };

        private void OnSalesCustomerPhaseChanged(SalesCustomerPhaseChanged message)
        {
            if (!IsEddi(message.CharacterId)) return;

            // Browsing is the first passive sales phase after DialogStep is completed by the dialogue UI.
            if (string.Equals(message.Phase, BrowsingPhase, StringComparison.Ordinal))
                _eddiBrowsing = true;
            if (string.Equals(message.Phase, DonePhase, StringComparison.Ordinal))
                _eddiLeft = true;
        }

        private void OnSalesPassiveSaleHappened(SalesPassiveSaleHappened message)
        {
            if (!IsEddi(message.CharacterId)) return;

            _eddiSold = true;
            _eddiSoldGenre = message.Genre;
        }

        private void OnSalesPassivePurchaseFailed(SalesPassivePurchaseFailed message)
        {
            if (!IsEddi(message.CharacterId)) return;

            _eddiFailed = true;
            _eddiFailedGenre = message.Genre;
        }

        private static bool IsEddi(string characterId)
            => string.Equals(characterId, EddiCharacterId, StringComparison.Ordinal);

        // Invariant that should always hold on day 1; a violation is a content/spawn defect, not player input.
        private void ReportEddiIncomplete()
        {
            Debug.LogError(
                $"{LogPrefix} day 1 completed without Eddi's scripted sale " +
                $"(browsed={_eddiBrowsing}, sold={_eddiSold}, failed={_eddiFailed}). " +
                "Eddi did not participate — check q_intro_eddi spawn and the day-1 shelf preset (TODO GAME-17).");

            _analytics?.TrackEvent(new AnalyticsEvent(EddiIncompleteEvent));
        }

        private Func<bool> Until(Func<bool> fact)
            => () => fact() || ResultsShown;

        private static string FormatGenreCallout(bool happened, string genre, string format)
        {
            if (!happened || string.IsNullOrEmpty(genre))
                return null;

            return string.Format(format, genre);
        }

        private void ResetLatch()
        {
            _eddiBrowsing = false;
            _eddiSold = false;
            _eddiFailed = false;
            _eddiLeft = false;
            _eddiSoldGenre = null;
            _eddiFailedGenre = null;
        }

        private void DisposeSubscriptions()
        {
            for (var i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }
    }
}
