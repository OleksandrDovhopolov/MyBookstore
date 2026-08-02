using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial;
using Game.Tutorial.API;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialAwaitWindowStep : ITutorialStep
    {
        private const int PollMs = 250;

        private readonly Func<bool> _predicate;
        private readonly int _timeoutMs;
        private readonly bool _failOpen;

        public TutorialAwaitWindowStep(
            string id,
            Func<bool> predicate,
            int timeoutMs = -1,
            bool failOpen = false)
        {
            Id = id;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _timeoutMs = timeoutMs;
            _failOpen = failOpen;
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            if (_timeoutMs <= 0)
            {
                while (!_predicate())
                    await UniTask.Delay(PollMs, cancellationToken: ct);

                return;
            }

            var elapsedMs = 0;
            while (!_predicate())
            {
                if (elapsedMs >= _timeoutMs)
                {
                    if (_failOpen)
                    {
                        Debug.LogWarning(
                            $"{TutorialLog.Prefix} await window step '{Id}' timed out after {_timeoutMs}ms; auto-advancing.");
                        return;
                    }

                    throw new TimeoutException(
                        $"{TutorialLog.Prefix} await window step '{Id}' timed out after {_timeoutMs}ms.");
                }

                await UniTask.Delay(PollMs, cancellationToken: ct);
                elapsedMs += PollMs;
            }
        }
    }
}
