using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Game.Journal.UI
{
    public sealed class SaveBackedJournalAttentionRepository : IJournalAttentionRepository
    {
        private readonly ISaveService _save;

        public SaveBackedJournalAttentionRepository(ISaveService save)
            => _save = save ?? throw new ArgumentNullException(nameof(save));

        public async UniTask<SavedJournalAttention> LoadAsync(CancellationToken ct)
        {
            var state = await _save.GetModuleAsync<SavedJournalAttention>(JournalAttentionSaveKeys.State, ct);
            return state ?? new SavedJournalAttention();
        }

        public UniTask SaveAsync(SavedJournalAttention state, CancellationToken ct)
            => _save.UpdateModuleAsync(
                JournalAttentionSaveKeys.State,
                state ?? new SavedJournalAttention(),
                JournalAttentionSaveKeys.StateSchemaVersion,
                ct);
    }
}
