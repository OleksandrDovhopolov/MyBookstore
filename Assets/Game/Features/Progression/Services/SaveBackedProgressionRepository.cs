using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Progression.API;
using Save;

namespace Game.Progression.Services
{
    /// <summary>
    /// MVP repository — local-only, backed by <see cref="ISaveService"/> module
    /// <see cref="ProgressionSaveKeys.State"/>.
    /// </summary>
    public sealed class SaveBackedProgressionRepository : IProgressionRepository
    {
        private readonly ISaveService _save;

        public SaveBackedProgressionRepository(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public async UniTask<ProgressionStateDto> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<ProgressionStateDto>(ProgressionSaveKeys.State, ct);
            return dto ?? new ProgressionStateDto();
        }

        public UniTask SaveAsync(ProgressionStateDto state, CancellationToken ct)
        {
            return _save.UpdateModuleAsync(
                ProgressionSaveKeys.State,
                state ?? new ProgressionStateDto(),
                ProgressionSaveKeys.StateSchemaVersion,
                ct);
        }
    }
}
