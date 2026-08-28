using System;

namespace Analytics
{
    /// <summary>
    /// First-run gate concerns: whether the consent screen must be shown, and how a decision is recorded.
    ///
    /// Deliberately separate from <see cref="IAnalyticsConsentService"/>, which is the read-side contract
    /// consumed per-event by CompositeAnalyticsService. One singleton implements both.
    /// </summary>
    public interface IConsentGateService
    {
        /// <summary>Policy version this build presents — see <see cref="ConsentPolicy.Version"/>.</summary>
        int PolicyVersion { get; }

        /// <summary>
        /// True when no decision is stored, or the stored decision predates the current policy version.
        /// </summary>
        bool IsDecisionRequired { get; }

        /// <summary>When the stored decision was made, or null if there is none.</summary>
        DateTime? DecidedAtUtc { get; }

        /// <summary>Records acceptance of every category and stamps the current policy version.</summary>
        void AcceptAll();

        /// <summary>
        /// Records a per-category decision. Used by a future settings screen (REL-2) where the player can
        /// accept some categories and decline others.
        /// </summary>
        void RecordDecision(bool analytics, bool attribution, bool personalizedAds);
    }
}
