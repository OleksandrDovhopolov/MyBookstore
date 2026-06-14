using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;

namespace Game.Inventory.Tests.Editor.Fakes
{
    /// <summary>
    /// In-memory fake of <see cref="IInventoryRepository"/>: keeps the last saved DTO and returns it
    /// from LoadAsync. Tracks save count for assertions on batching behavior.
    /// </summary>
    public sealed class FakeInventoryRepository : IInventoryRepository
    {
        public InventoryStateDto Stored { get; set; } = new();
        public int SaveCallCount { get; private set; }

        public UniTask<InventoryStateDto> LoadAsync(CancellationToken ct)
            => UniTask.FromResult(Clone(Stored));

        public UniTask SaveAsync(InventoryStateDto state, CancellationToken ct)
        {
            Stored = Clone(state) ?? new InventoryStateDto();
            SaveCallCount++;
            return UniTask.CompletedTask;
        }

        private static InventoryStateDto Clone(InventoryStateDto dto)
        {
            if (dto == null) return null;
            var copy = new InventoryStateDto();
            if (dto.Uniques != null)
                foreach (var u in dto.Uniques)
                    copy.Uniques.Add(new InventoryStateDto.UniqueEntry { ItemId = u.ItemId, CategoryId = u.CategoryId });
            if (dto.Stacks != null)
                foreach (var s in dto.Stacks)
                    copy.Stacks.Add(new InventoryStateDto.StackEntry { ItemId = s.ItemId, CategoryId = s.CategoryId, Count = s.Count });
            return copy;
        }
    }
}
