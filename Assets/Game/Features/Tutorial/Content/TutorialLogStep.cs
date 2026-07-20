using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialLogStep : ITutorialStep
    {
        private readonly string _message;

        public TutorialLogStep(string id, string message)
        {
            Id = id;
            _message = message;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Debug.Log(_message);
            return UniTask.CompletedTask;
        }
    }
}
