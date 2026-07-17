using System;
using System.Collections.Generic;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using MessagePipe;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDayOne : ITutorialSequence
    {
        private const string EddiCharacterId = "eddi";
        private const string BrowsingPhase = "Browsing";
        private const string EddiSearchText =
            "Eddi is looking for a Fact book. Watch how the shelf and sale chance work together.";
        private const string EddiSoldText =
            "Nice - Eddi bought a {0} book. A good genre match makes passive sales much more likely.";
        private const string EddiFailedText =
            "That {0} attempt missed. Even interested customers can walk away when the sale chance does not roll your way.";
        private const string WrapUpText =
            "Day complete - nice work! From tomorrow you'll stock the shelf and choose where to trade yourself.";
        private const string BottomPlacement = "bottom";
        private static readonly TimeSpan EddiFactTimeout = TimeSpan.FromSeconds(12);
        private static readonly TimeSpan CalloutReadDelay = TimeSpan.FromSeconds(3);

        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;
        private readonly IDayProgressService _dayProgress;
        private readonly ISubscriber<SalesCustomerPhaseChanged> _phaseSub;
        private readonly ISubscriber<SalesPassiveSaleHappened> _saleSub;
        private readonly ISubscriber<SalesPassivePurchaseFailed> _failSub;
        private readonly List<IDisposable> _subscriptions = new();
        private bool _eddiBrowsing;
        private bool _eddiSold;
        private bool _eddiFailed;
        private string _eddiSoldGenre;
        private string _eddiFailedGenre;

        public TutorialDayOne(
            TutorialOverlayController overlay,
            IUIManager ui,
            IDayProgressService dayProgress,
            ISubscriber<SalesCustomerPhaseChanged> phaseSub,
            ISubscriber<SalesPassiveSaleHappened> saleSub,
            ISubscriber<SalesPassivePurchaseFailed> failSub)
        {
            _overlay = overlay;
            _ui = ui;
            _dayProgress = dayProgress;
            _phaseSub = phaseSub;
            _saleSub = saleSub;
            _failSub = failSub;
        }

        public string Id => "tutorial_day_1";
        public int Priority => 10;
        public TutorialContext Context => TutorialContext.Location;
        public TutorialTrigger Trigger => TutorialTrigger.LocationLoaded;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible() => _dayProgress.Current.CurrentDay == 1;

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
            DisposeSubscriptions();
            ResetLatch();
            _overlay.HideCallout();
        }

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialAwaitFactStep("await_eddi_dialogue_complete", () => _eddiBrowsing),
                new TutorialShowCalloutStep("callout_search", _overlay, EddiSearchText, BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_sale", () => _eddiSold, EddiFactTimeout),
                new TutorialShowCalloutStep(
                    "callout_sold",
                    _overlay,
                    () => FormatGenreCallout(_eddiSold, _eddiSoldGenre, EddiSoldText),
                    BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_fail", () => _eddiFailed, EddiFactTimeout),
                new TutorialShowCalloutStep(
                    "callout_failed",
                    _overlay,
                    () => FormatGenreCallout(_eddiFailed, _eddiFailedGenre, EddiFailedText),
                    BottomPlacement),
                new TutorialDelayStep("callout_read_delay", CalloutReadDelay),
                new TutorialHideCalloutStep("hide_eddi_callout", _overlay),
                new TutorialAwaitWindowStep("wait_results_window", () => _ui.IsWindowShown<ResultsWindow>()),
                new TutorialShowTextStep("wrap_up", _overlay, _ui, WrapUpText, BottomPlacement),
            };

        private void OnSalesCustomerPhaseChanged(SalesCustomerPhaseChanged message)
        {
            if (!IsEddi(message.CharacterId)) return;

            // Browsing is the first passive sales phase after DialogStep is completed by the dialogue UI.
            if (string.Equals(message.Phase, BrowsingPhase, StringComparison.Ordinal))
                _eddiBrowsing = true;
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
