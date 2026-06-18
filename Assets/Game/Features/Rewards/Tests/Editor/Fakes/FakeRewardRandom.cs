using System.Collections.Generic;
using Game.Rewards.Services;

namespace Game.Rewards.Tests.Editor.Fakes
{
    /// <summary>Deterministic <see cref="IRewardRandom"/> fake. Queues drive results; an empty queue
    /// falls back to a configured default so tests don't need to enqueue values for paths they don't
    /// care about.</summary>
    internal sealed class FakeRewardRandom : IRewardRandom
    {
        private readonly Queue<double> _doubles = new();
        private readonly Queue<int> _ints = new();

        public double DefaultDouble { get; set; } = 0.0;
        public int DefaultInt { get; set; } = 0;

        public FakeRewardRandom EnqueueDouble(double v) { _doubles.Enqueue(v); return this; }
        public FakeRewardRandom EnqueueInt(int v) { _ints.Enqueue(v); return this; }

        public int Range(int minInclusive, int maxExclusive) =>
            _ints.Count > 0 ? _ints.Dequeue() : DefaultInt;

        public double NextDouble() =>
            _doubles.Count > 0 ? _doubles.Dequeue() : DefaultDouble;
    }
}
