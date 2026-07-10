using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LocationVisits.API;
using Save;

namespace Game.LocationVisits.Services
{
    /// <summary>
    /// Local-only repository backed by <see cref="ISaveService"/> module
    /// <see cref="LocationVisitsSaveKeys.State"/>. Mirrors <c>SaveBackedSalesStatsRepository</c>.
    /// </summary>
    public sealed class SaveBackedLocationVisitsRepository : ILocationVisitsRepository
    {
        private readonly ISaveService _save;

        public SaveBackedLocationVisitsRepository(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public async UniTask<LocationVisitsStateDto> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<LocationVisitsStateDto>(LocationVisitsSaveKeys.State, ct);
            return dto ?? new LocationVisitsStateDto();
        }

        public UniTask SaveAsync(LocationVisitsStateDto state, CancellationToken ct)
        {
            return _save.UpdateModuleAsync(
                LocationVisitsSaveKeys.State,
                state ?? new LocationVisitsStateDto(),
                LocationVisitsSaveKeys.StateSchemaVersion,
                ct);
        }
    }
}
