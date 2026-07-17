using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialAwaitFactStep : ITutorialStep
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly Func<bool> _fact;
        private readonly TimeSpan _timeout;

        public TutorialAwaitFactStep(string id, Func<bool> fact, TimeSpan timeout)
        {
            Id = id;
            _fact = fact;
            _timeout = timeout;
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            if (_fact?.Invoke() == true)
                return;

            var waitFact = UniTask.WaitUntil(() => _fact?.Invoke() == true, cancellationToken: ct);
            var timeout = UniTask.Delay(_timeout, cancellationToken: ct);
            var winner = await UniTask.WhenAny(waitFact, timeout);
            if (winner == 1)
                Debug.LogWarning($"{LogPrefix} await fact step '{Id}' timed out; auto-advancing.");
        }
    }
}
