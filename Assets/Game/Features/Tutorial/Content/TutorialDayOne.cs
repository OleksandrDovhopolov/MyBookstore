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
        private const string WelcomeText =
            "Welcome to your bookshop! You're open for your first day - customers buy books on their own as they browse.";
        private const string SaleChanceText =
            "Each sale depends on the genre's sale chance. Keep the shelf stocked with genres your visitors like.";
        private const string EddiSearchText =
            "Eddi is looking for a Fact book. Watch how the shelf and sale chance work together.";
        private const string EddiSoldText =
            "Nice - Eddi bought a Fact book. A good genre match makes passive sales much more likely.";
        private const string EddiFailedText =
            "That Travel attempt missed. Even interested customers can walk away when the sale chance does not roll your way.";
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
                new TutorialShowTextStep("welcome", _overlay, _ui, WelcomeText, BottomPlacement),
                new TutorialShowTextStep("sale_chance", _overlay, _ui, SaleChanceText, BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_browse", () => _eddiBrowsing, EddiFactTimeout),
                new TutorialShowCalloutStep("callout_search", _overlay, EddiSearchText, BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_sale", () => _eddiSold, EddiFactTimeout),
                new TutorialShowCalloutStep("callout_sold", _overlay, EddiSoldText, BottomPlacement),
                new TutorialAwaitFactStep("await_eddi_fail", () => _eddiFailed, EddiFactTimeout),
                new TutorialShowCalloutStep("callout_failed", _overlay, EddiFailedText, BottomPlacement),
                new TutorialDelayStep("callout_read_delay", CalloutReadDelay),
                new TutorialHideCalloutStep("hide_eddi_callout", _overlay),
                new TutorialAwaitWindowStep("wait_results_window", () => _ui.IsWindowShown<ResultsWindow>()),
                new TutorialShowTextStep("wrap_up", _overlay, _ui, WrapUpText, BottomPlacement),
            };

        private void OnSalesCustomerPhaseChanged(SalesCustomerPhaseChanged message)
        {
            if (!IsEddi(message.CharacterId)) return;
            if (string.Equals(message.Phase, BrowsingPhase, StringComparison.Ordinal))
                _eddiBrowsing = true;
        }

        private void OnSalesPassiveSaleHappened(SalesPassiveSaleHappened message)
        {
            if (IsEddi(message.CharacterId))
                _eddiSold = true;
        }

        private void OnSalesPassivePurchaseFailed(SalesPassivePurchaseFailed message)
        {
            if (IsEddi(message.CharacterId))
                _eddiFailed = true;
        }

        private static bool IsEddi(string characterId)
            => string.Equals(characterId, EddiCharacterId, StringComparison.Ordinal);

        private void ResetLatch()
        {
            _eddiBrowsing = false;
            _eddiSold = false;
            _eddiFailed = false;
        }

        private void DisposeSubscriptions()
        {
            for (var i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }
    }
}
