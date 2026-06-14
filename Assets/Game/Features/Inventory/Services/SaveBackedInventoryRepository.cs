using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Save;

namespace Game.Inventory.Services
{
    /// <summary>
    /// MVP repository — local-only, backed by <see cref="ISaveService"/> module
    /// <see cref="InventorySaveKeys.State"/>. A future <c>HttpInventoryRepository</c> can replace it
    /// without changes to <see cref="InventoryService"/> or its consumers.
    /// </summary>
    public sealed class SaveBackedInventoryRepository : IInventoryRepository
    {
        private readonly ISaveService _save;

        public SaveBackedInventoryRepository(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public async UniTask<InventoryStateDto> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<InventoryStateDto>(InventorySaveKeys.State, ct);
            return dto ?? new InventoryStateDto();
        }

        public UniTask SaveAsync(InventoryStateDto state, CancellationToken ct)
        {
            return _save.UpdateModuleAsync(
                InventorySaveKeys.State,
                state ?? new InventoryStateDto(),
                InventorySaveKeys.StateSchemaVersion,
                ct);
        }
    }
}
