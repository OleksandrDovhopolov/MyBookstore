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
            Debug.Log("[FirebaseRemoteConfigService] RC fetched and activated.");
        }

        public bool TryGetString(string key, out string value)
        {
            value = null;
            if (!_ready)
                return false;

            var v = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            value = v?.StringValue;
            return !string.IsNullOrEmpty(value);
        }
    }
}
#endif
