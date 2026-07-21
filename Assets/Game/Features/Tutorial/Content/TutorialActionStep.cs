using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    public sealed class TutorialActionStep : ITutorialStep
    {
        private readonly Action _action;

        public TutorialActionStep(string id, Action action)
        {
            Id = id;
            _action = action;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _action?.Invoke();
            return UniTask.CompletedTask;
        }
    }
}
