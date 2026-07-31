using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Cysharp.Threading.Tasks;
using Save;

namespace Book.Sell.Services
{
    /// <summary>
    /// Save-backed fire-once memory for scripted dialogues.
    /// Committed ids are persisted in save; deferred ids live only until the sales-day commit.
    /// </summary>
    public sealed class SaveBackedDeliveredDialoguesService : IDeliveredDialoguesService
    {
        private readonly ISaveService _save;
        private readonly HashSet<string> _pending = new();
        private HashSet<string> _committed;

        public SaveBackedDeliveredDialoguesService(ISaveService save)
            => _save = save ?? throw new ArgumentNullException(nameof(save));

        public event Action Changed;

        public bool IsDelivered(string dialogueId)
        {
            if (string.IsNullOrWhiteSpace(dialogueId)) return false;
            return EnsureLoaded().Contains(dialogueId) || _pending.Contains(dialogueId);
        }

        public async UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dialogueId)) return;

            var committed = EnsureLoaded();
            if (!committed.Add(dialogueId)) return;

            await PersistCommittedAsync(ct);
            Changed?.Invoke();
        }

        public UniTask MarkDeliveredDeferredAsync(string dialogueId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(dialogueId) && _pending.Add(dialogueId))
                Changed?.Invoke();

            return UniTask.CompletedTask;
        }

        public async UniTask CommitAsync(CancellationToken ct)
        {
            if (_pending.Count == 0) return;

            var committed = EnsureLoaded();
            var changed = false;
            foreach (var dialogueId in _pending)
                changed |= committed.Add(dialogueId);

            if (changed)
                await PersistCommittedAsync(ct);

            _pending.Clear();
            if (changed)
                Changed?.Invoke();
        }

        public void DiscardDeferred()
        {
            var changed = _pending.Count > 0;
            _pending.Clear();
            if (changed)
                Changed?.Invoke();
        }

        private async UniTask PersistCommittedAsync(CancellationToken ct)
        {
            await _save.UpdateModuleAsync(
                DialoguesSaveKeys.Delivered,
                new DeliveredDialogues { Ids = new List<string>(EnsureLoaded()) },
                DialoguesSaveKeys.DeliveredSchemaVersion,
                ct);
        }

        private HashSet<string> EnsureLoaded()
        {
            if (_committed != null) return _committed;

            var dto = _save
                .GetModuleAsync<DeliveredDialogues>(DialoguesSaveKeys.Delivered, CancellationToken.None)
                .GetAwaiter().GetResult();

            _committed = dto?.Ids != null ? new HashSet<string>(dto.Ids) : new HashSet<string>();
            return _committed;
        }
    }
}
