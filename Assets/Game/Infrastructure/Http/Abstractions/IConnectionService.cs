using System;

namespace Game.Http {
    /// <summary>
    /// Тонкий сервис соединения, от которого зависят REST-команды.
    /// Прячет конкретный HTTP-бэкенд (UnityWebRequest/иной) и логику проверки интернета.
    /// </summary>
    public interface IConnectionService {
        bool IsConnected { get; }
        ConnectionCheckBehaviour CheckInternetBehaviour { get; }

        IRequest CreateRequest(IRequestParams p);

        void HandleNoInternet(Action callbackOnAvailable);
        void SubscribeOnceOnInternetBecomeAvailable(Action callback);
        void UnsubscribeFromInternetBecomeAvailable(Action callback);
    }
}
