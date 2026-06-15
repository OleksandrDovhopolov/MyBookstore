using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.WorldHud
{
    public interface IWorldHud
    {
        Transform Target { get; }
        WorldHudArgs Args { get; }
        bool IsAttached { get; }
        GameObject GameObject { get; }

        UniTask DetachAsync(CancellationToken ct = default);
    }
}
