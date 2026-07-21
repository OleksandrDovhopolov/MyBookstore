using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    /// <summary>
    /// Terminal invariant check. If <paramref name="condition"/> is false when the step is reached,
    /// invokes <paramref name="onViolated"/> (log / analytics) and completes anyway — it never blocks the run.
    /// Because steps do not execute on cancel, this fires only on a real completion path, so it does not need
    /// to distinguish "completed" from "aborted".
    /// </summary>
    public sealed class TutorialAssertStep : ITutorialStep
    {
        private readonly Func<bool> _condition;
        private readonly Action _onViolated;

        public TutorialAssertStep(string id, Func<bool> condition, Action onViolated)
        {
            Id = id;
            _condition = condition;
            _onViolated = onViolated;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            if (_condition?.Invoke() != true)
                _onViolated?.Invoke();

            return UniTask.CompletedTask;
        }
    }
}
