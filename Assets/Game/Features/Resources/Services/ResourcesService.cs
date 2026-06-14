using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Resources.API;
using Save;
using UnityEngine;

namespace Game.Resources.Services
{
    /// <summary>
    /// Default <see cref="IResourcesService"/> implementation. Acts as an <see cref="ISaveHook"/>:
    /// on AfterLoadAsync it pulls the persisted state from <see cref="IResourcesRepository"/> into
    /// an in-memory dictionary; writes go to the repository and update the cache in lockstep.
    /// </summary>
    public sealed class ResourcesService : IResourcesService, ISaveHook
    {
        private const string LogPrefix = "[Resources]";

        private readonly IResourcesRepository _repository;
        private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);

        private bool _loaded;

        public ResourcesService(ISaveService save, IResourcesRepository repository)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            save.RegisterHook(this);
        }

        public event Action<ResourceChangeEvent> Changed;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);
            _amounts.Clear();
            if (dto.Amounts != null)
            {
                foreach (var kv in dto.Amounts)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    _amounts[kv.Key] = kv.Value;
                }
            }
            _loaded = true;
            Debug.Log($"{LogPrefix} loaded: {_amounts.Count} resources.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- sync read -----

        public IReadOnlyDictionary<string, int> GetAll() => _amounts;

        public int GetAmount(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return 0;
            return _amounts.TryGetValue(resourceId, out var amount) ? amount : 0;
        }

        public bool Has(string resourceId, int amount)
        {
            if (amount <= 0) return true;
            return GetAmount(resourceId) >= amount;
        }

        // ----- async write -----

        public async UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(resourceId) || amount <= 0) return;
            if (!_loaded)
                Debug.LogWarning($"{LogPrefix} Add before AfterLoadAsync; mutation will still apply.");

            var oldAmount = GetAmount(resourceId);
            var newAmount = oldAmount + amount;
            _amounts[resourceId] = newAmount;

            await _repository.SaveAsync(BuildDto(), ct);

            Debug.Log($"{LogPrefix} {resourceId} {oldAmount} → {newAmount} (+{amount}) reason={reason}");
            Changed?.Invoke(new ResourceChangeEvent(resourceId, oldAmount, newAmount, amount, reason));
        }

        public async UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(resourceId)) return false;
            if (amount <= 0) return true;

            var oldAmount = GetAmount(resourceId);
            if (oldAmount < amount) return false;

            var newAmount = oldAmount - amount;
            _amounts[resourceId] = newAmount;

            await _repository.SaveAsync(BuildDto(), ct);

            Debug.Log($"{LogPrefix} {resourceId} {oldAmount} → {newAmount} (-{amount}) reason={reason}");
            Changed?.Invoke(new ResourceChangeEvent(resourceId, oldAmount, newAmount, -amount, reason));
            return true;
        }

        private ResourcesStateDto BuildDto()
        {
            var dto = new ResourcesStateDto();
            foreach (var kv in _amounts) dto.Amounts[kv.Key] = kv.Value;
            return dto;
        }
    }
}
