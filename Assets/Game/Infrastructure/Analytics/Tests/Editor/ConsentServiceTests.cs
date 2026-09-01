using System;
using Analytics;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AnalyticsTests.Editor
{
    public sealed class ConsentServiceTests
    {
        [Test]
        public void IsDecisionRequired_CleanInstall_ReturnsTrue()
        {
            var service = new ConsentService(new FakeConsentStore());

            Assert.That(service.IsDecisionRequired, Is.True);
            Assert.That(service.DecidedAtUtc, Is.Null);
        }

        [Test]
        public void Defaults_CleanInstall_AllConsentFlagsAreFalse()
        {
            var service = new ConsentService(new FakeConsentStore());

            Assert.That(service.CanSendAnalytics, Is.False);
            Assert.That(service.CanSendAttributionData, Is.False);
            Assert.That(service.CanSendPersonalizedAdsData, Is.False);
        }

        [Test]
        public void IsDecisionRequired_AfterAcceptAll_ReturnsFalse()
        {
            var service = new ConsentService(new FakeConsentStore());

            service.AcceptAll();

            Assert.That(service.IsDecisionRequired, Is.False);
        }

        [Test]
        public void AcceptAll_SetsAllConsentFlagsTrue()
        {
            var service = new ConsentService(new FakeConsentStore());

            service.AcceptAll();

            Assert.That(service.CanSendAnalytics, Is.True);
            Assert.That(service.CanSendAttributionData, Is.True);
            Assert.That(service.CanSendPersonalizedAdsData, Is.True);
        }

        [Test]
        public void AcceptAll_PersistsCurrentPolicyVersionAndTimestamp()
        {
            var store = new FakeConsentStore();
            var service = new ConsentService(store);
            var before = DateTime.UtcNow.Ticks;

            service.AcceptAll();

            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Saved.PolicyVersion, Is.EqualTo(ConsentPolicy.Version));
            Assert.That(store.Saved.DecidedAtUtcTicks, Is.GreaterThanOrEqualTo(before));
            Assert.That(service.DecidedAtUtc, Is.Not.Null);
        }

        [Test]
        public void IsDecisionRequired_StoredPolicyVersionIsOlder_ReturnsTrue()
        {
            var store = FakeConsentStore.WithRecord(
                new ConsentRecord(ConsentPolicy.Version - 1, true, true, true, DateTime.UtcNow.Ticks));

            var service = new ConsentService(store);

            Assert.That(service.IsDecisionRequired, Is.True);
        }

        [Test]
        public void CanSendAnalytics_StoredPolicyVersionIsOlder_ReturnsFalse()
        {
            // Consent granted against an older policy must not carry over — it is revoked until the
            // player accepts the new one.
            var store = FakeConsentStore.WithRecord(
                new ConsentRecord(ConsentPolicy.Version - 1, true, true, true, DateTime.UtcNow.Ticks));

            var service = new ConsentService(store);

            Assert.That(service.CanSendAnalytics, Is.False);
            Assert.That(service.CanSendAttributionData, Is.False);
            Assert.That(service.CanSendPersonalizedAdsData, Is.False);
        }

        [Test]
        public void IsDecisionRequired_StoredPolicyVersionIsNewer_ReturnsFalse()
        {
            // The player rolled back to an older build. Re-prompting for a policy this build cannot show
            // would be worse than honouring the decision they already made.
            var store = FakeConsentStore.WithRecord(
                new ConsentRecord(ConsentPolicy.Version + 1, true, false, false, DateTime.UtcNow.Ticks));

            var service = new ConsentService(store);

            Assert.That(service.IsDecisionRequired, Is.False);
            Assert.That(service.CanSendAnalytics, Is.True);
        }

        [Test]
        public void RecordDecision_PartialConsent_PersistsEachFlagIndependently()
        {
            var store = new FakeConsentStore();
            var service = new ConsentService(store);

            service.RecordDecision(analytics: true, attribution: false, personalizedAds: false);

            Assert.That(service.CanSendAnalytics, Is.True);
            Assert.That(service.CanSendAttributionData, Is.False);
            Assert.That(service.CanSendPersonalizedAdsData, Is.False);
            Assert.That(store.Saved.Analytics, Is.True);
            Assert.That(store.Saved.Attribution, Is.False);
            Assert.That(store.Saved.PersonalizedAds, Is.False);
        }

        [Test]
        public void RecordDecision_AllFalse_DisablesAnalytics()
        {
            var store = new FakeConsentStore();
            var service = new ConsentService(store);

            service.RecordDecision(analytics: false, attribution: false, personalizedAds: false);

            Assert.That(service.CanSendAnalytics, Is.False);
            Assert.That(service.CanSendAttributionData, Is.False);
            Assert.That(service.CanSendPersonalizedAdsData, Is.False);
            Assert.That(store.Saved.Analytics, Is.False);
        }

        [Test]
        public void SetAnalyticsConsent_False_RevokesOnlyAnalytics()
        {
            var service = new ConsentService(new FakeConsentStore());
            service.AcceptAll();

            service.SetAnalyticsConsent(false);

            Assert.That(service.CanSendAnalytics, Is.False);
            Assert.That(service.CanSendAttributionData, Is.True);
            Assert.That(service.CanSendPersonalizedAdsData, Is.True);
        }

        [Test]
        public void IsDecisionRequired_StoreLoadFails_ReturnsTrue()
        {
            // Fail closed: an unreadable store means nothing is collected and the player is asked again.
            // ConsentService logs the failure loudly, which would otherwise fail the test.
            LogAssert.ignoreFailingMessages = true;
            var service = new ConsentService(new ThrowingConsentStore());

            Assert.That(service.IsDecisionRequired, Is.True);
            Assert.That(service.CanSendAnalytics, Is.False);
            LogAssert.ignoreFailingMessages = false;
        }

        private sealed class FakeConsentStore : IConsentStore
        {
            private ConsentRecord _record;
            private bool _hasRecord;

            public ConsentRecord Saved { get; private set; }
            public int SaveCount { get; private set; }

            public static FakeConsentStore WithRecord(ConsentRecord record) =>
                new() { _record = record, _hasRecord = true };

            public bool TryLoad(out ConsentRecord record)
            {
                record = _record;
                return _hasRecord;
            }

            public void Save(in ConsentRecord record)
            {
                _record = record;
                _hasRecord = true;
                Saved = record;
                SaveCount++;
            }
        }

        private sealed class ThrowingConsentStore : IConsentStore
        {
            public bool TryLoad(out ConsentRecord record) =>
                throw new InvalidOperationException("store unavailable");

            public void Save(in ConsentRecord record) =>
                throw new InvalidOperationException("store unavailable");
        }
    }
}
