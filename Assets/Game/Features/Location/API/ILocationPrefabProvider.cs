using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Location.API
{
    public interface ILocationPrefabProvider
    {
        string LastPreloadedLocationId { get; }

        UniTask<GameObject> PreloadAsync(string locationId, CancellationToken ct);

        GameObject GetPreloaded(string locationId);
        bool IsPreloaded(string locationId);
    }
}
