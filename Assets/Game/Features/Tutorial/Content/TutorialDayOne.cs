using System;
using System.Collections.Generic;
using Analytics;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using Game.UI.ContentWidget;
using Infrastructure.TutorialUI;
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
            "The client selects books from genres of interest to him.";
        private const string EddiSoldText =
            "If the customer finds the book he needs, he continues shopping.";
        private const string EddiFailedText =
            "If not, then he leaves the store.";
        private const string GenrePanelTargetId = "location.genre_book_count_panel";
        private const string Text1 = "The presence of books in the genres themselves does not guarantee sales.";
        private const string Text2 =
            "The more books you have on your shelves in a particular genre, the higher your chance of selling them.";
        private const string Text3 = "Click on a book to find out its chance of sale.";
        private const string HighlightText = "Tap a genre to inspect sale chance.";
        private const string Text4 =
            "This is the chance a book of that genre will sell. Stock more of a genre to raise it.";
        private const string WrapUpText =
            "Day complete - nice work! From tomorrow you'll stock the shelf and choose where to trade yourself.";
        private const string BottomPlacement = "bottom";
        private const string LogPrefix = "[Tutorial]";
        private const string EddiIncompleteEvent = "tutorial_day1_eddi_incomplete";
        private const string PassiveFailMissingEvent = "tutorial_day1_wave2_passive_fail_missing";

        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;
        private readonly IDayProgressService _dayProgress;
        private readonly ITutorialTargetRegistry _targets;
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
        private bool _postEddiPassiveFailed;
        private string _eddiSoldGenre;
        private string _eddiFailedGenre;

        public TutorialDayOne(
            TutorialOverlayController overlay,
            IUIManager ui,
            IDayProgressService dayProgress,
            ITutorialTargetRegistry targets,
            ISubscriber<SalesCustomerPhaseChanged> phaseSub,
            ISubscriber<SalesPassiveSaleHappened> saleSub,
            ISubscriber<SalesPassivePurchaseFailed> failSub,
            IPublisher<SalesPauseRequested> pausePublisher,
            IAnalyticsService analytics)
        {
            _overlay = overlay;
            _ui = ui;
            _dayProgress = dayProgress;
            _targets = targets;
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
            _overlay.HideHighlight();
            HideContentWidgetIfShown();
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
                new TutorialBlockingCalloutStep(
                    "callout_sold",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _eddiSold,
                    () => FormatGenreCallout(_eddiSold, _eddiSoldGenre, EddiSoldText),
                    BottomPlacement,
                    hideTextAfterTap: true),
                new TutorialAwaitFactStep("await_eddi_fail", Until(() => _eddiFailed)),
                new TutorialBlockingCalloutStep(
                    "callout_failed",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _eddiFailed,
                    () => FormatGenreCallout(_eddiFailed, _eddiFailedGenre, EddiFailedText),
                    BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_left", Until(() => _eddiLeft)),
                new TutorialHideCalloutStep("hide_eddi_callout", _overlay),
                // Eddi's scripted beats are the whole point of day 1. If the guaranteed sale never landed
                // (Eddi absent, or Fact missing from the shelf — a content desync, see TODO GAME-17), the
                // day still completes cleanly, but the lesson silently did not happen. Report it.
                new TutorialAssertStep("verify_eddi_participated", () => _eddiSold, ReportEddiIncomplete),
                new TutorialAwaitFactStep("await_passive_fail", Until(() => _postEddiPassiveFailed)),
                new TutorialAssertStep("verify_passive_fail", () => _postEddiPassiveFailed, ReportPassiveFailMissing),
                new TutorialBlockingCalloutStep(
                    "text_1",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _postEddiPassiveFailed,
                    () => Text1,
                    BottomPlacement),
                new TutorialBlockingCalloutStep(
                    "text_2",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _postEddiPassiveFailed,
                    () => Text2,
                    BottomPlacement),
                new TutorialBlockingCalloutStep(
                    "text_3",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _postEddiPassiveFailed,
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
                    () => _postEddiPassiveFailed,
                    TutorialPointerPlacement.Left,
                    _pausePublisher,
                    pauseSales: true),
                new TutorialBlockingCalloutStep(
                    "text_4",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _postEddiPassiveFailed,
                    () => Text4,
                    BottomPlacement,
                    hideTextAfterTap: true,
                    dimBackground: false,
                    lockUi: false),
                new TutorialHideWindowStep<ContentWidgetController>("hide_sale_chance_widget", _ui),
                new TutorialAwaitWindowStep("wait_results_window", () => ResultsShown),
                new TutorialBlockingCalloutStep(
                    "wrap_up",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => true,
                    () => WrapUpText,
                    BottomPlacement),
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
            if (!IsEddi(message.CharacterId))
            {
                _postEddiPassiveFailed = true;
                return;
            }

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

        private void ReportPassiveFailMissing()
        {
            Debug.LogError(
                $"{LogPrefix} day 1 reached results without a non-Eddi passive purchase failure. " +
                "Check day2_missed_sale and the day-1 wave setup.");

            _analytics?.TrackEvent(new AnalyticsEvent(PassiveFailMissingEvent));
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
            _postEddiPassiveFailed = false;
            _eddiSoldGenre = null;
            _eddiFailedGenre = null;
        }

        private void DisposeSubscriptions()
        {
            for (var i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }

        private void HideContentWidgetIfShown()
        {
            HideWindowIfShown<ContentWidgetController>();
        }

        private void HideWindowIfShown<TWindow>()
            where TWindow : class, IWindowController
        {
            if (_ui == null || !_ui.IsWindowShown<TWindow>())
                return;

            _ui.HideAsync<TWindow>(forceClose: true).Forget();
        }
    }
}
