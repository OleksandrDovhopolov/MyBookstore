using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using Save;

namespace Game.Resources.Services
{
    /// <summary>
    /// MVP repository — local-only, backed by <see cref="ISaveService"/> module
    /// <see cref="ResourcesSaveKeys.State"/>.
    /// </summary>
    public sealed class SaveBackedResourcesRepository : IResourcesRepository
    {
        private readonly ISaveService _save;

        public SaveBackedResourcesRepository(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public async UniTask<ResourcesStateDto> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<ResourcesStateDto>(ResourcesSaveKeys.State, ct);
            return dto ?? new ResourcesStateDto();
        }

        public UniTask SaveAsync(ResourcesStateDto state, CancellationToken ct)
        {
            return _save.UpdateModuleAsync(
                ResourcesSaveKeys.State,
                state ?? new ResourcesStateDto(),
                ResourcesSaveKeys.StateSchemaVersion,
                ct);
        }
    }
}
