using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Commands {
    /// <summary>
    /// Одноразовый таймер на UniTask: срабатывает один раз через заданное число миллисекунд
    /// на игровом потоке (player loop). Используется для таймаутов команд.
    /// </summary>
    internal sealed class OneShotTimer {
        private readonly int _milliseconds;
        private CancellationTokenSource _cts;

        public event Action OnTimePassed;

        public OneShotTimer(int milliseconds) {
            _milliseconds = milliseconds;
        }

        public void Start() {
            StopAndDispose();
            _cts = new CancellationTokenSource();
            Run(_cts.Token).Forget();
        }

        private async UniTaskVoid Run(CancellationToken token) {
            try {
                await UniTask.Delay(_milliseconds, ignoreTimeScale: true, cancellationToken: token);
                if (!token.IsCancellationRequested) {
                    OnTimePassed?.Invoke();
                }
            } catch (OperationCanceledException) {
                // таймер остановлен — это нормально
            }
        }

        public void StopAndDispose() {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
