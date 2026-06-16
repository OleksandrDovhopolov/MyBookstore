using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shop.API;
using Save;

namespace Game.Shop.Services
{
    /// <summary>
    /// MVP repository — local-only, backed by <see cref="ISaveService"/> module
    /// <see cref="ShopSaveKeys.State"/>. Mirrors <c>SaveBackedResourcesRepository</c>.
    /// </summary>
    public sealed class SaveBackedShopRepository
    {
        private readonly ISaveService _save;

        public SaveBackedShopRepository(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public async UniTask<ShopStateDto> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<ShopStateDto>(ShopSaveKeys.State, ct);
            return dto ?? new ShopStateDto();
        }

        public UniTask SaveAsync(ShopStateDto state, CancellationToken ct)
        {
            return _save.UpdateModuleAsync(
                ShopSaveKeys.State,
                state ?? new ShopStateDto(),
                ShopSaveKeys.StateSchemaVersion,
                ct);
        }
    }
}
