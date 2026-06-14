using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;

namespace Game.DayCycle.Tests.Editor.Fakes
{
    /// <summary>In-memory IResourcesService for tests — no save / no repository.</summary>
    public sealed class FakeResourcesService : IResourcesService
    {
        private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);

        public event Action<ResourceChangeEvent> Changed;

        public IReadOnlyDictionary<string, int> GetAll() => _amounts;

        public int GetAmount(string resourceId)
            => string.IsNullOrEmpty(resourceId) ? 0 : (_amounts.TryGetValue(resourceId, out var a) ? a : 0);

        public bool Has(string resourceId, int amount)
            => amount <= 0 || GetAmount(resourceId) >= amount;

        public UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(resourceId) || amount <= 0) return UniTask.CompletedTask;
            var old = GetAmount(resourceId);
            _amounts[resourceId] = old + amount;
            Changed?.Invoke(new ResourceChangeEvent(resourceId, old, old + amount, amount, reason));
            return UniTask.CompletedTask;
        }

        public UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(resourceId)) return UniTask.FromResult(false);
            if (amount <= 0) return UniTask.FromResult(true);
            var old = GetAmount(resourceId);
            if (old < amount) return UniTask.FromResult(false);
            _amounts[resourceId] = old - amount;
            Changed?.Invoke(new ResourceChangeEvent(resourceId, old, old - amount, -amount, reason));
            return UniTask.FromResult(true);
        }
    }
}
