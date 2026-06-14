using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Progression.API;

namespace Game.DayCycle.Tests.Editor.Fakes
{
    /// <summary>In-memory IProgressionService for tests — clamps reputation ≥ 0.</summary>
    public sealed class FakeProgressionService : IProgressionService
    {
        public int Reputation { get; private set; }

        public event Action<ProgressionChangeEvent> Changed;

        public UniTask AddReputationAsync(int delta, string reason, CancellationToken ct)
        {
            if (delta == 0) return UniTask.CompletedTask;
            var old = Reputation;
            var next = Math.Max(0, old + delta);
            var effective = next - old;
            if (effective == 0) return UniTask.CompletedTask;
            Reputation = next;
            Changed?.Invoke(new ProgressionChangeEvent(old, next, effective, reason));
            return UniTask.CompletedTask;
        }

        /// <summary>Test convenience — seed reputation without going through AddAsync.</summary>
        public void SetReputation(int value) => Reputation = Math.Max(0, value);
    }
}
