using System;
using System.Globalization;
using UnityEngine;

namespace Analytics
{
    /// <summary>
    /// PlayerPrefs-backed consent storage.
    ///
    /// PlayerPrefs rather than ISaveService on purpose: the consent decision has to be readable during
    /// phase_technical_init, long before SaveDataLoadOperation runs in phase_data_load.
    ///
    /// The ".v1" suffix on every key is the STORAGE LAYOUT version — bump it only if the set of keys or
    /// their types change. The privacy policy version is the *value* stored under PolicyVersionKey; see
    /// <see cref="ConsentPolicy"/>.
    /// </summary>
    public sealed class PlayerPrefsConsentStore : IConsentStore
    {
        private const string PolicyVersionKey = "consent.policy_version.v1";
        private const string AnalyticsKey = "consent.analytics.v1";
        private const string AttributionKey = "consent.attribution.v1";
        private const string PersonalizedAdsKey = "consent.personalized_ads.v1";
        private const string DecidedAtUtcTicksKey = "consent.decided_at_utc_ticks.v1";

        /// <summary>Every key this store owns. Used by the editor "Reset Consent" tool.</summary>
        public static readonly string[] AllKeys =
        {
            PolicyVersionKey,
            AnalyticsKey,
            AttributionKey,
            PersonalizedAdsKey,
            DecidedAtUtcTicksKey
        };

        public bool TryLoad(out ConsentRecord record)
        {
            record = default;

            // No policy version key means no decision was ever recorded.
            if (!PlayerPrefs.HasKey(PolicyVersionKey))
            {
                return false;
            }

            record = new ConsentRecord(
                PlayerPrefs.GetInt(PolicyVersionKey, 0),
                PlayerPrefs.GetInt(AnalyticsKey, 0) == 1,
                PlayerPrefs.GetInt(AttributionKey, 0) == 1,
                PlayerPrefs.GetInt(PersonalizedAdsKey, 0) == 1,
                ReadTicks());

            return true;
        }

        public void Save(in ConsentRecord record)
        {
            PlayerPrefs.SetInt(PolicyVersionKey, record.PolicyVersion);
            PlayerPrefs.SetInt(AnalyticsKey, record.Analytics ? 1 : 0);
            PlayerPrefs.SetInt(AttributionKey, record.Attribution ? 1 : 0);
            PlayerPrefs.SetInt(PersonalizedAdsKey, record.PersonalizedAds ? 1 : 0);

            // PlayerPrefs has no long overload, so ticks go in as an invariant string.
            PlayerPrefs.SetString(
                DecidedAtUtcTicksKey,
                record.DecidedAtUtcTicks.ToString(CultureInfo.InvariantCulture));

            PlayerPrefs.Save();
        }

        private static long ReadTicks()
        {
            var raw = PlayerPrefs.GetString(DecidedAtUtcTicksKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return 0L;
            }

            // A corrupt timestamp must not discard an otherwise valid decision — the flags are what
            // gate collection; the timestamp is only for auditing.
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                Debug.LogWarning($"[Consent] Ignoring unparsable stored timestamp '{raw}'.");
                return 0L;
            }

            if (ticks < 0L || ticks > DateTime.MaxValue.Ticks)
            {
                Debug.LogWarning($"[Consent] Ignoring out-of-range stored timestamp '{raw}'.");
                return 0L;
            }

            return ticks;
        }
    }
}
