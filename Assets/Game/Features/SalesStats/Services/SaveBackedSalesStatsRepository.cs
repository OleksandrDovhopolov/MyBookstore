using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.SalesStats.API;
using Save;

namespace Game.SalesStats.Services
{
    /// <summary>
    /// MVP repository — local-only, backed by <see cref="ISaveService"/> module
    /// <see cref="SalesStatsSaveKeys.State"/>. Mirrors <c>SaveBackedProgressionRepository</c>.
    /// </summary>
    public sealed class SaveBackedSalesStatsRepository : ISalesStatsRepository
    {
        private readonly ISaveService _save;

        public SaveBackedSalesStatsRepository(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public async UniTask<SalesStatsStateDto> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<SalesStatsStateDto>(SalesStatsSaveKeys.State, ct);
            return dto ?? new SalesStatsStateDto();
        }

        public UniTask SaveAsync(SalesStatsStateDto state, CancellationToken ct)
        {
            return _save.UpdateModuleAsync(
                SalesStatsSaveKeys.State,
                state ?? new SalesStatsStateDto(),
                SalesStatsSaveKeys.StateSchemaVersion,
                ct);
        }
    }
}
