using System;
using Analytics;
using Cysharp.Threading.Tasks;
using Game.Privacy.Services;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Privacy
{
    public sealed class ConsentWindowArgs : WindowArgs
    {
        public ConsentWindowArgs()
        {
            // System layer, not the Popup default of Additional: this gate is legally blocking and must
            // draw above the loading screen and anything else on screen.
            AsSystem();
        }
    }

    /// <summary>
    /// First-run privacy notice and terms acknowledgement (REL-5). Shown once by ConsentGateOperation
    /// before Remote Config initialises, and again only if <see cref="ConsentPolicy.Version"/> is bumped.
    /// </summary>
    [Window("ConsentWindow", WindowType.Popup, keepInCache: false)]
    public sealed class ConsentWindowController : WindowController<ConsentWindowView>
    {
        private IConsentGateService _gate;
        private PrivacyLinkSettings _links;

        [Inject]
        public void Construct(IConsentGateService gate, PrivacyLinkSettings links)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _links = links ?? throw new ArgumentNullException(nameof(links));
        }

        protected override void OnInit()
        {
            View.ContinueClick += OnContinueClicked;
            View.PrivacyLinkClick += OnPrivacyLinkClicked;
            View.SetAnalyticsConsent(true);

            // Degrade rather than brick the boot: an unconfigured URL hides the link. The build-time
            // PrivacyLinksBuildCheck is what actually stops a release from shipping without one.
            if (!_links.HasPrivacyPolicyUrl)
            {
                Debug.LogError(
                    "[Consent] Privacy policy URL is not configured on BootstrapInstaller. " +
                    "The Privacy & Terms link is hidden. This must be set before release.");
                View.SetPrivacyLinkVisible(false);
            }
        }

        protected override void OnShowStart()
        {
            View.SetContinueInteractable(true);
        }

        protected override void OnDispose()
        {
            if (View == null) return;

            View.ContinueClick -= OnContinueClicked;
            View.PrivacyLinkClick -= OnPrivacyLinkClicked;
        }

        private void OnPrivacyLinkClicked()
        {
            if (!_links.HasPrivacyPolicyUrl) return;

            Application.OpenURL(_links.PrivacyPolicyUrl);
        }

        private void OnContinueClicked()
        {
            // Guard against a double tap while the close animation plays.
            View.SetContinueInteractable(false);

            // Persist BEFORE closing: ConsentGateOperation re-checks IsDecisionRequired once the window
            // closes, and would otherwise race the write.
            _gate.RecordDecision(analytics: View.AnalyticsConsent, attribution: false, personalizedAds: false);

            CloseAsync(View.destroyCancellationToken).Forget();
        }
    }
}
