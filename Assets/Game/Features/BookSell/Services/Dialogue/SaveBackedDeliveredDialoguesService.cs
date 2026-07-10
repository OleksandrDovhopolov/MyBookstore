using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Book.Sell.Services
{
    /// <summary>
    /// <see cref="IDeliveredDialoguesService"/> backed by an <see cref="ISaveService"/> module
    /// (<see cref="DialoguesSaveKeys.Delivered"/>). Mirrors <c>SaveBackedQuestsRepository</c>.
    ///
    /// The set is read lazily and cached in memory. The read is synchronous
    /// (<c>GetModuleAsync(...).GetAwaiter().GetResult()</c>) — same assumption/hack as
    /// <c>PreparationSalesSetupProvider</c>: after <c>ISaveService.LoadAsync</c> the modules are in memory,
    /// and every caller here (day-start spawner, dialogue-close marker) runs well after load.
    /// </summary>
    public sealed class SaveBackedDeliveredDialoguesService : IDeliveredDialoguesService
    {
        private readonly ISaveService _save;
        private HashSet<string> _cache;

        public SaveBackedDeliveredDialoguesService(ISaveService save)
            => _save = save ?? throw new ArgumentNullException(nameof(save));

        public bool IsDelivered(string dialogueId)
        {
            if (string.IsNullOrWhiteSpace(dialogueId)) return false;
            return EnsureLoaded().Contains(dialogueId);
        }

        public async UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dialogueId)) return;

            var set = EnsureLoaded();
            if (!set.Add(dialogueId)) return;   // already delivered — no save churn

            await _save.UpdateModuleAsync(
                DialoguesSaveKeys.Delivered,
                new DeliveredDialogues { Ids = new List<string>(set) },
                DialoguesSaveKeys.DeliveredSchemaVersion,
                ct);
        }

        private HashSet<string> EnsureLoaded()
        {
            if (_cache != null) return _cache;

            var dto = _save
                .GetModuleAsync<DeliveredDialogues>(DialoguesSaveKeys.Delivered, CancellationToken.None)
                .GetAwaiter().GetResult();

            _cache = dto?.Ids != null ? new HashSet<string>(dto.Ids) : new HashSet<string>();
            return _cache;
        }
    }
}
