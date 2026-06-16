using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;

namespace Game.Rewards.Tests.Editor.Fakes
{
    internal sealed class FakeResourcesService : IResourcesService
    {
        private readonly Dictionary<string, int> _amounts = new Dictionary<string, int>();

        public List<(string id, int amount, string reason)> AddCalls { get; } =
            new List<(string id, int amount, string reason)>();

        public event Action<ResourceChangeEvent> Changed;

        public IReadOnlyDictionary<string, int> GetAll() => _amounts;

        public int GetAmount(string resourceId) =>
            resourceId != null && _amounts.TryGetValue(resourceId, out var v) ? v : 0;

        public bool Has(string resourceId, int amount) => GetAmount(resourceId) >= amount;

        public UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            AddCalls.Add((resourceId, amount, reason));
            var old = GetAmount(resourceId);
            _amounts[resourceId] = old + amount;
            Changed?.Invoke(new ResourceChangeEvent(resourceId, old, old + amount, amount, reason));
            return UniTask.CompletedTask;
        }

        public UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            var old = GetAmount(resourceId);
            if (old < amount) return UniTask.FromResult(false);
            _amounts[resourceId] = old - amount;
            Changed?.Invoke(new ResourceChangeEvent(resourceId, old, old - amount, -amount, reason));
            return UniTask.FromResult(true);
        }
    }
}
