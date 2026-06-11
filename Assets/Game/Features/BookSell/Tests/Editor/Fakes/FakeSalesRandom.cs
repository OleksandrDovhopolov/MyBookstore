using System.Collections.Generic;
using Book.Sell.Services;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>
    /// Deterministic fake: feeds a fixed queue of values for NextDouble and Range.
    /// If the queue is empty, falls back to a "definite fail" (NextDouble = 0.99, Range = min)
    /// so tests fail explicitly instead of passing by accident.
    /// </summary>
    public sealed class FakeSalesRandom : ISalesRandom
    {
        private readonly Queue<double> _doubles = new();
        private readonly Queue<int> _rangeOffsets = new(); // relative index in [0, count)

        public FakeSalesRandom EnqueueDouble(params double[] values)
        {
            foreach (var v in values) _doubles.Enqueue(v);
            return this;
        }

        public FakeSalesRandom EnqueueRangeIndex(params int[] indices)
        {
            foreach (var i in indices) _rangeOffsets.Enqueue(i);
            return this;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (_rangeOffsets.Count == 0) return minInclusive;
            var off = _rangeOffsets.Dequeue();
            var size = maxExclusive - minInclusive;
            if (size <= 0) return minInclusive;
            // clamp into range
            if (off < 0) off = 0;
            if (off >= size) off = size - 1;
            return minInclusive + off;
        }

        public double NextDouble()
            => _doubles.Count > 0 ? _doubles.Dequeue() : 0.99d;
    }
}
