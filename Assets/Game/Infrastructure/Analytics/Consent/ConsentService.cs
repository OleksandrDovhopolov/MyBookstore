using System;
using UnityEngine;

namespace Analytics
{
    /// <summary>
    /// The real consent service: persists the player's decision and gates the analytics categories on it.
    /// Replaces <see cref="StubAnalyticsConsentService"/>, which is a test double only.
    /// </summary>
    public sealed class ConsentService : IAnalyticsConsentService, IConsentGateService
    {
        private readonly IConsentStore _store;

        private ConsentRecord _record;
        private bool _hasRecord;

        public ConsentService(IConsentStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Reload();
        }

        public int PolicyVersion => ConsentPolicy.Version;

        /// <summary>
        /// No stored decision, or one made against an older policy. A decision stored against a NEWER
        /// policy version (the player rolled back to an older build) still stands — re-prompting them for
        /// a policy this build cannot show would be worse than honouring what they already agreed to.
        /// </summary>
        public bool IsDecisionRequired => !_hasRecord || _record.PolicyVersion < ConsentPolicy.Version;

        public DateTime? DecidedAtUtc => _hasRecord ? _record.DecidedAtUtc : null;

        // Every category reads false until a decision valid for the current policy exists. Bumping the
        // policy version therefore revokes consent immediately, until the player accepts again.
        public bool CanSendAnalytics => !IsDecisionRequired && _record.Analytics;
        public bool CanSendAttributionData => !IsDecisionRequired && _record.Attribution;
        public bool CanSendPersonalizedAdsData => !IsDecisionRequired && _record.PersonalizedAds;

        public void AcceptAll() => RecordDecision(true, true, true);

        public void RecordDecision(bool analytics, bool attribution, bool personalizedAds)
        {
            _record = new ConsentRecord(
                ConsentPolicy.Version,
                analytics,
                attribution,
                personalizedAds,
                DateTime.UtcNow.Ticks);
            _hasRecord = true;

            try
            {
                _store.Save(_record);
            }
            catch (Exception ex)
            {
                // The in-memory decision stands for this session so the player is not blocked, but the
                // gate will ask again next launch. Losing the write is worth a loud log.
                Debug.LogError($"[Consent] Failed to persist the consent decision: {ex}");
            }
        }

        public void SetAnalyticsConsent(bool isAllowed) =>
            RecordDecision(isAllowed, _record.Attribution, _record.PersonalizedAds);

        public void SetAttributionConsent(bool isAllowed) =>
            RecordDecision(_record.Analytics, isAllowed, _record.PersonalizedAds);

        public void SetPersonalizedAdsConsent(bool isAllowed) =>
            RecordDecision(_record.Analytics, _record.Attribution, isAllowed);

        private void Reload()
        {
            try
            {
                _hasRecord = _store.TryLoad(out _record);
            }
            catch (Exception ex)
            {
                // Fail closed: an unreadable store is treated as "no decision", so nothing is collected
                // and the player is asked again.
                Debug.LogError($"[Consent] Failed to read stored consent, treating as undecided: {ex}");
                _hasRecord = false;
                _record = default;
            }
        }
    }
}
