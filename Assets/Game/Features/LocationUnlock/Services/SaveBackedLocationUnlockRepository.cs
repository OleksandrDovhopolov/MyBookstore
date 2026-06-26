using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LocationUnlock.API;
using Save;

namespace Game.LocationUnlock.Services
{
    /// <summary>MVP repository — local-only, backed by save module <see cref="LocationUnlockSaveKeys.State"/>.</summary>
    public sealed class SaveBackedLocationUnlockRepository : ILocationUnlockRepository
    {
        private readonly ISaveService _save;

        public SaveBackedLocationUnlockRepository(ISaveService save)
            => _save = save ?? throw new ArgumentNullException(nameof(save));

        public async UniTask<LocationUnlockStateDto> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<LocationUnlockStateDto>(LocationUnlockSaveKeys.State, ct);
            return dto ?? new LocationUnlockStateDto();
        }

        public UniTask SaveAsync(LocationUnlockStateDto state, CancellationToken ct)
            => _save.UpdateModuleAsync(
                LocationUnlockSaveKeys.State,
                state ?? new LocationUnlockStateDto(),
                LocationUnlockSaveKeys.StateSchemaVersion,
                ct);
    }
}
