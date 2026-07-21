using System;
using Cysharp.Threading.Tasks;
using Game.UI;
using MessagePipe;
using VContainer.Unity;

namespace Book.Sell.Services
{
    public sealed class SalesInteractionPauseBridge : IStartable, IDisposable
    {
        private readonly IInteractionLock _lock;
        private readonly ISubscriber<SalesPauseRequested> _subscriber;
        private readonly object _token = new();

        private IDisposable _subscription;
        private bool _pauseRequested;
        private int _pauseVersion;

        public SalesInteractionPauseBridge(IInteractionLock interactionLock, ISubscriber<SalesPauseRequested> subscriber)
        {
            _lock = interactionLock ?? throw new ArgumentNullException(nameof(interactionLock));
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        }

        public void Start()
        {
            _subscription = _subscriber.Subscribe(OnPauseRequested);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
            Resume();
        }

        private void OnPauseRequested(SalesPauseRequested message)
        {
            if (message.Paused)
                Pause();
            else
                Resume();
        }

        private void Pause()
        {
            if (_pauseRequested)
                return;

            _pauseRequested = true;
            var version = ++_pauseVersion;
            AcquireWhenAvailableAsync(version).Forget();
        }

        private async UniTaskVoid AcquireWhenAvailableAsync(int version)
        {
            while (_pauseRequested && version == _pauseVersion)
            {
                if (_lock.TryAcquire(_token))
                    return;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private void Resume()
        {
            _pauseRequested = false;
            _pauseVersion++;
            _lock.Release(_token);
        }
    }
}
