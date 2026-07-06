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
        UniTask AnimateAmountToAsync(int amount, CancellationToken ct = default);
        void PlayArriveFeedback();
    }
}
