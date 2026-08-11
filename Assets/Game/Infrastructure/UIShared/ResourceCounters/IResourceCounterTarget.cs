using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UIShared
{
    public interface IResourceCounterTarget
    {
        string ResourceId { get; }
        RectTransform RectTransform { get; }
        int DisplayedAmount { get; }

        void SetAmountImmediate(int amount);

        /// <summary>Count-up after a coin flight (day payout). Slow ramp, tuned on the target.</summary>
        UniTask AnimateAmountToAsync(int amount, CancellationToken ct = default);

        /// <summary>
        /// Ordinary balance change — a purchase, a reward. Same ramp, its own (shorter) duration, and
        /// it runs in both directions.
        /// </summary>
        UniTask AnimateChangeAsync(int amount, CancellationToken ct = default);

        void PlayArriveFeedback();
    }
}
