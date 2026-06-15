using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Decor
{
    public interface IDecorPlacementService
    {
        IReadOnlyList<DecorPlacementEntry> GetAllPlacements();
        string GetDecorInSlot(string slotId);
        IReadOnlyList<string> GetActiveDecorIds();

        UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct);
        UniTask UnplaceAsync(string slotId, CancellationToken ct);
        UniTask ClearAllAsync(CancellationToken ct);

        event Action PlacementChanged;
    }
}
