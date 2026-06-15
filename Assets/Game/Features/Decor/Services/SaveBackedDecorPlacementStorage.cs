using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Game.Decor.Services
{
    /// <summary>
    /// MVP storage: persists DecorPlacementState through ISaveService module "decor.placement".
    /// A future HttpDecorPlacementStorage can swap in without touching DecorPlacementService.
    /// </summary>
    public sealed class SaveBackedDecorPlacementStorage
    {
        private readonly ISaveService _save;

        public SaveBackedDecorPlacementStorage(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public async UniTask<DecorPlacementState> LoadAsync(CancellationToken ct)
        {
            var state = await _save.GetModuleAsync<DecorPlacementState>(DecorSaveKeys.Placement, ct);
            return state ?? new DecorPlacementState();
        }

        public UniTask SaveAsync(DecorPlacementState state, CancellationToken ct)
        {
            return _save.UpdateModuleAsync(
                DecorSaveKeys.Placement,
                state ?? new DecorPlacementState(),
                DecorSaveKeys.PlacementSchemaVersion,
                ct);
        }
    }
}
