using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    public sealed class TutorialAsyncActionStep : ITutorialStep
    {
        private readonly Func<CancellationToken, UniTask> _action;

        public TutorialAsyncActionStep(string id, Func<CancellationToken, UniTask> action)
        {
            Id = id;
            _action = action;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
            => _action != null ? _action(ct) : UniTask.CompletedTask;
    }
}
