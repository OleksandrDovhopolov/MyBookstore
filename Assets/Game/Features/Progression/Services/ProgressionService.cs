using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Progression.API;
using Save;
using UnityEngine;

namespace Game.Progression.Services
{
    /// <summary>
    /// Default <see cref="IProgressionService"/> implementation. Same lifecycle as
    /// <c>ResourcesService</c> / <c>InventoryService</c>: self-registers as <see cref="ISaveHook"/>,
    /// loads from repository on AfterLoadAsync, writes through the repository on every mutation.
    /// </summary>
    public sealed class ProgressionService : IProgressionService, ISaveHook
    {
        private const string LogPrefix = "[Progression]";

        private readonly IProgressionRepository _repository;
        private int _reputation;
        private bool _loaded;

        public ProgressionService(ISaveService save, IProgressionRepository repository)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            save.RegisterHook(this);
        }

        public int Reputation => _reputation;

        public event Action<ProgressionChangeEvent> Changed;

        // ----- ISaveHook -----

        public async UniTask AfterLoadAsync(CancellationToken ct)
        {
            var dto = await _repository.LoadAsync(ct);
            _reputation = Math.Max(0, dto.Reputation);
            _loaded = true;
            Debug.Log($"{LogPrefix} loaded: reputation={_reputation}.");
        }

        public UniTask BeforeSaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        // ----- write -----

        public async UniTask AddReputationAsync(int delta, string reason, CancellationToken ct)
        {
            if (delta == 0) return;
            if (!_loaded)
                Debug.LogWarning($"{LogPrefix} Add before AfterLoadAsync; mutation will still apply.");

            var oldRep = _reputation;
            var newRep = Math.Max(0, oldRep + delta);
            var effectiveDelta = newRep - oldRep;
            if (effectiveDelta == 0) return;

            _reputation = newRep;

            await _repository.SaveAsync(new ProgressionStateDto { Reputation = _reputation }, ct);

            Debug.Log($"{LogPrefix} reputation {oldRep} → {newRep} ({(effectiveDelta >= 0 ? "+" : "")}{effectiveDelta}) reason={reason}");
            Changed?.Invoke(new ProgressionChangeEvent(oldRep, newRep, effectiveDelta, reason));
        }
    }
}
