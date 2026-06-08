using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Http {
    /// <summary>
    /// Дефолтная реализация IConnectionService поверх UnityWebRequest-фабрики.
    /// Проверка интернета — по Application.internetReachability (замени на реальный пинг при необходимости).
    /// </summary>
    public class ConnectionService : IConnectionService {
        private readonly IRequestFactory _requestFactory;

        public ConnectionCheckBehaviour CheckInternetBehaviour { get; set; } = ConnectionCheckBehaviour.ErrorLogsWithComplete;

        private event Action _onAvailable;
        private bool _monitoring;

        public ConnectionService(IRequestFactory requestFactory) {
            _requestFactory = requestFactory;
        }

        public bool IsConnected => Application.internetReachability != NetworkReachability.NotReachable;

        public IRequest CreateRequest(IRequestParams p) => _requestFactory.CreateRequest(p);

        public void HandleNoInternet(Action callbackOnAvailable) {
            SubscribeOnceOnInternetBecomeAvailable(callbackOnAvailable);
        }

        public void SubscribeOnceOnInternetBecomeAvailable(Action callback) {
            if (callback == null) {
                return;
            }
            _onAvailable += callback;
            StartMonitor();
        }

        public void UnsubscribeFromInternetBecomeAvailable(Action callback) {
            _onAvailable -= callback;
        }

        private void StartMonitor() {
            if (_monitoring) {
                return;
            }
            _monitoring = true;
            Monitor().Forget();
        }

        private async UniTaskVoid Monitor() {
            while (_onAvailable != null) {
                await UniTask.Delay(1000, ignoreTimeScale: true);
                if (IsConnected) {
                    var callbacks = _onAvailable;
                    _onAvailable = null;
                    callbacks?.Invoke();
                }
            }
            _monitoring = false;
        }
    }
}
