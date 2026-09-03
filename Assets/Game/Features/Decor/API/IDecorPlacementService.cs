using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Decor
{
    public readonly struct DecorPlacementChange
    {
        public DecorPlacementChange(string decorId, string slotId, string action)
        {
            DecorId = decorId;
            SlotId = slotId;
            Action = action;
        }

        public string DecorId { get; }
        public string SlotId { get; }
        public string Action { get; }
    }

    public interface IDecorPlacementService
    {
        IReadOnlyList<DecorPlacementEntry> GetAllPlacements();
        string GetDecorInSlot(string slotId);
        IReadOnlyList<string> GetActiveDecorIds();

        UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct);
        UniTask<DecorPlacementResult> ReplaceAsync(string decorId, string slotId, CancellationToken ct);
        UniTask UnplaceAsync(string slotId, CancellationToken ct);
        UniTask ClearAllAsync(CancellationToken ct);

        event Action PlacementChanged;
        event Action<DecorPlacementChange> PlacementActionPerformed;
    }
}
