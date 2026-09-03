using System;
using Analytics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnalyticsTests.Editor
{
    public sealed class PlayerPrefsConsentStoreTests
    {
        private const string DecidedAtUtcTicksKey = "consent.decided_at_utc_ticks.v1";

        [SetUp]
        [TearDown]
        public void ClearConsentKeys()
        {
            foreach (var key in PlayerPrefsConsentStore.AllKeys)
            {
                PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.Save();
        }

        [Test]
        public void TryLoad_NoKeys_ReturnsFalse()
        {
            var store = new PlayerPrefsConsentStore();

            Assert.That(store.TryLoad(out _), Is.False);
        }

        [Test]
        public void SaveThenTryLoad_RoundTripsAllFields()
        {
            var store = new PlayerPrefsConsentStore();
            var ticks = DateTime.UtcNow.Ticks;
            var saved = new ConsentRecord(ConsentPolicy.Version, true, false, true, ticks);

            store.Save(saved);

            Assert.That(store.TryLoad(out var loaded), Is.True);
            Assert.That(loaded.PolicyVersion, Is.EqualTo(ConsentPolicy.Version));
            Assert.That(loaded.Analytics, Is.True);
            Assert.That(loaded.Attribution, Is.False);
            Assert.That(loaded.PersonalizedAds, Is.True);
            Assert.That(loaded.DecidedAtUtcTicks, Is.EqualTo(ticks));
            Assert.That(loaded.DecidedAtUtc, Is.Not.Null);
        }

        [Test]
        public void TryLoad_CorruptTicksValue_StillLoadsFlagsWithNullTimestamp()
        {
            var store = new PlayerPrefsConsentStore();
            store.Save(new ConsentRecord(ConsentPolicy.Version, true, true, false, DateTime.UtcNow.Ticks));

            PlayerPrefs.SetString(DecidedAtUtcTicksKey, "not-a-number");
            PlayerPrefs.Save();

            // The store warns about the unparsable value; the decision itself must survive.
            LogAssert.ignoreFailingMessages = true;
            var found = store.TryLoad(out var loaded);
            LogAssert.ignoreFailingMessages = false;

            Assert.That(found, Is.True);
            Assert.That(loaded.Analytics, Is.True);
            Assert.That(loaded.Attribution, Is.True);
            Assert.That(loaded.PersonalizedAds, Is.False);
            Assert.That(loaded.DecidedAtUtc, Is.Null);
        }
    }
}
