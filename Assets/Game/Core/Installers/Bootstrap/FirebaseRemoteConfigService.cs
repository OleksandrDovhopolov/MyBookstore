// Firebase-specific Remote Config implementation.
// Enabled by the BOOKSTORE_FIREBASE_RC define AFTER importing FirebaseRemoteConfig_*.unitypackage
// (Project Settings -> Player -> Scripting Define Symbols) and switching the registration
// in ConfigsVContainerBindings. Until then NullRemoteConfigService is active — the build does not break.
#if BOOKSTORE_FIREBASE_RC
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase.RemoteConfig;
using Game.Configs.Remote;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class FirebaseRemoteConfigService : IRemoteConfigService
    {
        private bool _ready;

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            var settings = new ConfigSettings { MinimumFetchIntervalInMilliseconds = 0 };
            await FirebaseRemoteConfig.DefaultInstance
                .SetConfigSettingsAsync(settings)
                .AsUniTask()
                .AttachExternalCancellation(ct);

            await FirebaseRemoteConfig.DefaultInstance
                .FetchAndActivateAsync()
                .AsUniTask()
                .AttachExternalCancellation(ct);

            _ready = true;

            var info = FirebaseRemoteConfig.DefaultInstance.Info;
            var keys = string.Join(", ", FirebaseRemoteConfig.DefaultInstance.Keys);
            Debug.Log(
                $"[FirebaseRemoteConfigService] activated. LastFetchStatus={info.LastFetchStatus}, " +
                $"FetchTime={info.FetchTime:O}, keys=[{keys}]");
        }

        public bool TryGetString(string key, out string value)
        {
            value = null;
            if (!_ready)
            {
                Debug.LogWarning($"[FirebaseRemoteConfigService] TryGetString('{key}') before InitializeAsync — RC is not activated yet.");
                return false;
            }

            var v = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            value = v.StringValue;

            // Source: RemoteValue = came from the server; StaticValue = the key is absent in RC.
            Debug.Log($"[FirebaseRemoteConfigService] key='{key}' source={v.Source} value='{value}'");
            return !string.IsNullOrEmpty(value);
        }
    }
}
#endif
