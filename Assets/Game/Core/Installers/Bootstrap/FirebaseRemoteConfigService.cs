// Firebase-специфичная реализация Remote Config.
// Включается define'ом BOOKSTORE_FIREBASE_RC ПОСЛЕ импорта FirebaseRemoteConfig_*.unitypackage
// (Project Settings → Player → Scripting Define Symbols) и переключения регистрации
// в ConfigsVContainerBindings. До этого активна NullRemoteConfigService — сборка не ломается.
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
                Debug.LogWarning($"[FirebaseRemoteConfigService] TryGetString('{key}') до InitializeAsync — RC ещё не активирован.");
                return false;
            }

            var v = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            value = v.StringValue;
            // Source: RemoteValue = пришло с сервера; StaticValue = ключа нет в RC вообще.
            Debug.Log($"[FirebaseRemoteConfigService] key='{key}' source={v.Source} value='{value}'");
            return !string.IsNullOrEmpty(value);
        }
    }
}
#endif
