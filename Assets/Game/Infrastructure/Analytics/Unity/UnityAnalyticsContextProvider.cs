using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Analytics
{
    public sealed class UnityAnalyticsContextProvider : IAnalyticsContextProvider, IAnalyticsUserContext
    {
        private const string InstallTimestampPrefsKey = "analytics.install_timestamp_utc_ticks.v1";
        private const string SessionNumberPrefsKey = "analytics.session_number.v1";

        private readonly IAnalyticsConfig _config;
        private readonly IPlayerIdentityProvider _playerIdentityProvider;
        private readonly string _sessionId;
        private readonly int _sessionNumber;
        private string _userId;

        public UnityAnalyticsContextProvider(IAnalyticsConfig config, IPlayerIdentityProvider playerIdentityProvider = null)
        {
            _config = config;
            _playerIdentityProvider = playerIdentityProvider;
            _sessionId = Guid.NewGuid().ToString("N");
            _sessionNumber = PlayerPrefs.GetInt(SessionNumberPrefsKey, 0) + 1;
            PlayerPrefs.SetInt(SessionNumberPrefsKey, _sessionNumber);
            EnsureInstallTimestamp();
        }

        public string UserId => _userId;

        public void SetUserId(string userId)
        {
            _userId = string.IsNullOrWhiteSpace(userId) ? null : userId;
        }

        public IReadOnlyDictionary<string, object> GetCommonParameters()
        {
            var installId = GetInstallId();
            var userId = string.IsNullOrWhiteSpace(_userId) ? installId : _userId;

            var parameters = new Dictionary<string, object>
            {
                [AnalyticsParameterNames.AppVersion] = Application.version,
                [AnalyticsParameterNames.BuildNumber] = GetBuildNumber(),
                [AnalyticsParameterNames.Platform] = Application.platform.ToString(),
                [AnalyticsParameterNames.DeviceModel] = SystemInfo.deviceModel,
                [AnalyticsParameterNames.OsVersion] = SystemInfo.operatingSystem,
                [AnalyticsParameterNames.Language] = Application.systemLanguage.ToString(),
                [AnalyticsParameterNames.Country] = GetCountry(),
                [AnalyticsParameterNames.InstallId] = installId,
                [AnalyticsParameterNames.SessionId] = _sessionId,
                [AnalyticsParameterNames.SessionNumber] = _sessionNumber,
                [AnalyticsParameterNames.DaysSinceInstall] = GetDaysSinceInstall(),
                [AnalyticsParameterNames.Environment] = _config.Environment
            };

            if (!string.IsNullOrWhiteSpace(userId))
            {
                parameters[AnalyticsParameterNames.UserId] = userId;
            }

            return parameters;
        }

        private string GetInstallId()
        {
            try
            {
                var installId = _playerIdentityProvider?.GetPlayerId();
                if (!string.IsNullOrWhiteSpace(installId))
                {
                    return installId;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AnalyticsContext] Failed to get install id: {exception.Message}");
            }

            return SystemInfo.deviceUniqueIdentifier;
        }

        private static string GetBuildNumber()
        {
#if UNITY_IOS
            return UnityEngine.iOS.Device.generation.ToString();
#elif UNITY_ANDROID
            return Application.version;
#else
            return Application.version;
#endif
        }

        private static string GetCountry()
        {
            try
            {
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private static void EnsureInstallTimestamp()
        {
            if (PlayerPrefs.HasKey(InstallTimestampPrefsKey))
            {
                return;
            }

            PlayerPrefs.SetString(InstallTimestampPrefsKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        private static int GetDaysSinceInstall()
        {
            var ticksString = PlayerPrefs.GetString(InstallTimestampPrefsKey, string.Empty);
            if (!long.TryParse(ticksString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return 0;
            }

            var installTime = new DateTime(ticks, DateTimeKind.Utc);
            return Mathf.Max(0, (int)(DateTime.UtcNow - installTime).TotalDays);
        }
    }
}
