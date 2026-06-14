using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;

namespace Game.Resources.Tests.Editor.Fakes
{
    public sealed class FakeResourcesRepository : IResourcesRepository
    {
        public ResourcesStateDto Stored { get; set; } = new();
        public int SaveCallCount { get; private set; }

        public UniTask<ResourcesStateDto> LoadAsync(CancellationToken ct)
            => UniTask.FromResult(Clone(Stored));

        public UniTask SaveAsync(ResourcesStateDto state, CancellationToken ct)
        {
            Stored = Clone(state) ?? new ResourcesStateDto();
            SaveCallCount++;
            return UniTask.CompletedTask;
        }

        private static ResourcesStateDto Clone(ResourcesStateDto dto)
        {
            if (dto == null) return null;
            var copy = new ResourcesStateDto();
            if (dto.Amounts != null)
                foreach (var kv in dto.Amounts) copy.Amounts[kv.Key] = kv.Value;
            return copy;
        }
    }
}
