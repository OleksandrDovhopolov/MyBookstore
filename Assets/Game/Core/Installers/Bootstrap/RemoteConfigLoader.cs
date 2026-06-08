using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Инициализация Firebase и (в будущем) загрузка Remote Config.
    /// Сейчас реализована только проверка зависимостей — этого достаточно
    /// для проверки подключения. FetchAndActivate требует импорта
    /// FirebaseRemoteConfig_*.unitypackage.
    /// </summary>
    public class RemoteConfigLoader
    {
        public bool IsReady { get; private set; }
        public bool DependenciesReady => _dependenciesReady;
        private bool _dependenciesReady;

        public async UniTask EnsureDependenciesAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_dependenciesReady)
            {
                return;
            }

            var dependencyStatus = await FirebaseApp
                .CheckAndFixDependenciesAsync()
                .AsUniTask()
                .AttachExternalCancellation(ct);

            if (dependencyStatus != DependencyStatus.Available)
            {
                throw new InvalidOperationException(
                    $"Firebase dependencies not available: {dependencyStatus}");
            }

            _dependenciesReady = true;
            Debug.Log("[RemoteConfigLoader] Firebase dependencies ready.");
        }

        // Реализация активна за define BOOKSTORE_FIREBASE_RC (после импорта
        // FirebaseRemoteConfig_*.unitypackage). До этого — безопасный no-op, чтобы
        // сборка не падала, если метод где-то вызван (см. FirebaseRemoteConfigService).
#if BOOKSTORE_FIREBASE_RC
        public async UniTask FetchAndActivateAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var settings = new Firebase.RemoteConfig.ConfigSettings { MinimumFetchIntervalInMilliseconds = 0 };
            await Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance
                .SetConfigSettingsAsync(settings).AsUniTask().AttachExternalCancellation(ct);
            await Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance
                .FetchAndActivateAsync().AsUniTask().AttachExternalCancellation(ct);

            IsReady = true;
            Debug.Log("[RemoteConfigLoader] Remote Config fetched and activated.");
        }
#else
        public UniTask FetchAndActivateAsync(CancellationToken ct)
        {
            Debug.LogWarning(
                "[RemoteConfigLoader] FetchAndActivateAsync skipped: define BOOKSTORE_FIREBASE_RC " +
                "не задан (FirebaseRemoteConfig SDK не импортирован).");
            return UniTask.CompletedTask;
        }
#endif
    }
}
