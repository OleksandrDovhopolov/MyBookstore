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

        // TODO: реализовать после импорта FirebaseRemoteConfig_*.unitypackage.
        // Оригинальный код использовал:
        //   using Firebase.RemoteConfig;
        //   var settings = new ConfigSettings { MinimumFetchIntervalInMilliseconds = 0 };
        //   await FirebaseRemoteConfig.DefaultInstance.SetConfigSettingsAsync(settings).AsUniTask();
        //   await FirebaseRemoteConfig.DefaultInstance.FetchAndActivateAsync().AsUniTask();
        //   IsReady = true;
        public UniTask FetchAndActivateAsync(CancellationToken ct)
        {
            throw new NotImplementedException(
                "Firebase Remote Config SDK не импортирован. " +
                "Импортируй FirebaseRemoteConfig_*.unitypackage и раскомментируй реализацию.");
        }
    }
}
