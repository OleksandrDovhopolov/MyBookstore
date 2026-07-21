using System;
using System.Collections.Generic;
using Analytics;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial;
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

        public string Id => TutorialSequenceIds.DayOne;
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
                TutorialAnalyticsSteps.Checkpoint(
                    "checkpoint_eddi_intro_start",
                    _analytics,
                    Id,
                    TutorialContent.Analytics.EddiIntroStage,
                    TutorialContent.Analytics.StateStart),
                new TutorialAwaitFactStep("await_eddi_dialogue_complete", Until(() => _eddiBrowsing)),
                new TutorialBlockingCalloutStep(
                    "callout_search",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _eddiBrowsing,
                    () => TutorialTexts.EddiSearch,
                    TutorialContent.Placements.Bottom),
                new TutorialAwaitFactStep("await_eddi_sale", Until(() => _eddiSold)),
                new TutorialBlockingCalloutStep(
                    "callout_sold",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _eddiSold,
                    () => FormatGenreCallout(_eddiSold, _eddiSoldGenre, TutorialTexts.EddiSold),
                    TutorialContent.Placements.Bottom,
                    hideTextAfterTap: true),
                new TutorialAwaitFactStep("await_eddi_fail", Until(() => _eddiFailed)),
                new TutorialBlockingCalloutStep(
                    "callout_failed",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _eddiFailed,
                    () => FormatGenreCallout(_eddiFailed, _eddiFailedGenre, TutorialTexts.EddiFailed),
                    TutorialContent.Placements.Bottom),
                new TutorialAwaitFactStep("await_eddi_left", Until(() => _eddiLeft)),
                new TutorialHideCalloutStep("hide_eddi_callout", _overlay),
                TutorialAnalyticsSteps.Checkpoint(
                    "checkpoint_eddi_intro_end",
                    _analytics,
                    Id,
                    TutorialContent.Analytics.EddiIntroStage,
                    TutorialContent.Analytics.StateEnd),
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
                    () => TutorialTexts.DayOneLessonBooksDoNotGuaranteeSales,
                    TutorialContent.Placements.Bottom),
                new TutorialBlockingCalloutStep(
                    "text_2",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _postEddiPassiveFailed,
                    () => TutorialTexts.DayOneLessonMoreBooksRaiseChance,
                    TutorialContent.Placements.Bottom),
                new TutorialBlockingCalloutStep(
                    "text_3",
                    _ui,
                    _overlay,
                    _pausePublisher,
                    () => _postEddiPassiveFailed,
                    () => TutorialTexts.DayOnePromptInspectSaleChance,
                    TutorialContent.Placements.Bottom),
                TutorialAnalyticsSteps.Checkpoint(
                    "checkpoint_sale_chance_start",
                    _analytics,
                    Id,
                    TutorialContent.Analytics.SaleChanceStage,
                    TutorialContent.Analytics.StateStart),
                new TutorialHighlightClickStep(
                    "click_genre_panel",
                    _overlay,
                    _targets,
                    TutorialTargetIds.LocationGenreBookCountPanel,
                    TutorialTexts.SaleChanceHighlight,
                    TutorialContent.Placements.Bottom,
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
                    () => TutorialTexts.DayOneLessonSaleChance,
                    TutorialContent.Placements.Bottom,
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
                    () => TutorialTexts.DayOneWrapUp,
                    TutorialContent.Placements.Bottom),
                new TutorialAwaitWindowStep("wait_results_closed", () => !ResultsShown),
            };

        private void OnSalesCustomerPhaseChanged(SalesCustomerPhaseChanged message)
        {
            if (!IsEddi(message.CharacterId)) return;

            // Browsing is the first passive sales phase after DialogStep is completed by the dialogue UI.
            if (string.Equals(message.Phase, TutorialContent.SalesPhases.Browsing, StringComparison.Ordinal))
                _eddiBrowsing = true;
            if (string.Equals(message.Phase, TutorialContent.SalesPhases.Done, StringComparison.Ordinal))
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
            => string.Equals(characterId, TutorialContent.Characters.Eddi, StringComparison.Ordinal);

        // Invariant that should always hold on day 1; a violation is a content/spawn defect, not player input.
        private void ReportEddiIncomplete()
        {
            Debug.LogError(
                $"{TutorialLog.Prefix} day 1 completed without Eddi's scripted sale " +
                $"(browsed={_eddiBrowsing}, sold={_eddiSold}, failed={_eddiFailed}). " +
                "Eddi did not participate — check q_intro_eddi spawn and the day-1 shelf preset (TODO GAME-17).");

            _analytics?.TrackEvent(new AnalyticsEvent(AnalyticsEventNames.TutorialDayOneEddiIncomplete));
        }

        private void ReportPassiveFailMissing()
        {
            Debug.LogError(
                $"{TutorialLog.Prefix} day 1 reached results without a non-Eddi passive purchase failure. " +
                "Check day2_missed_sale and the day-1 wave setup.");

            _analytics?.TrackEvent(new AnalyticsEvent(AnalyticsEventNames.TutorialDayOnePassiveFailMissing));
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
