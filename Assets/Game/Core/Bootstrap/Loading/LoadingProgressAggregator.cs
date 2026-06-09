using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Bootstrap.Loading
{
    public sealed class LoadingProgressAggregator
    {
        private readonly float _smoothingFactor;
        private float _smoothedProgress;

        public LoadingProgressAggregator(float smoothingFactor = 0.25f)
        {
            _smoothingFactor = Math.Clamp(smoothingFactor, 0.01f, 1f);
        }

        public float SmoothedProgress => _smoothedProgress;

        public void Reset()
        {
            _smoothedProgress = 0f;
        }

        public float Update(IReadOnlyCollection<ILoadingOperation> operations)
        {
            if (operations == null || operations.Count == 0)
            {
                return _smoothedProgress;
            }

            var totalWeight = operations.Sum(x => x.Weight <= 0f ? 1f : x.Weight);
            if (totalWeight <= 0f)
            {
                return _smoothedProgress;
            }

            var weightedRaw = operations.Sum(x =>
            {
                var weight = x.Weight <= 0f ? 1f : x.Weight;
                var operationProgress = Math.Clamp(x.Progress, 0f, 1f);
                return operationProgress * weight;
            }) / totalWeight;

            var next = _smoothedProgress + (weightedRaw - _smoothedProgress) * _smoothingFactor;
            _smoothedProgress = Math.Clamp(Math.Max(_smoothedProgress, next), 0f, 1f);
            return _smoothedProgress;
        }
    }
}
