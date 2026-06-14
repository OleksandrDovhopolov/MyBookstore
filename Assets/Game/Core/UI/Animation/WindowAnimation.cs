using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.UI
{
    public abstract class WindowAnimation : MonoBehaviour
    {
        public abstract float DefaultDuration { get; }
        public abstract UniTask PlayInAsync(CancellationToken ct);
        public abstract UniTask PlayOutAsync(CancellationToken ct);
    }
}
