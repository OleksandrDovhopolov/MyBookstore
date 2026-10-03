using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Game.Attention
{
    public sealed class SaveBackedAttentionRepository : IAttentionRepository
    {
        private readonly ISaveService _save;

        public SaveBackedAttentionRepository(ISaveService save)
            => _save = save ?? throw new ArgumentNullException(nameof(save));

        public async UniTask<SavedAttention> LoadAsync(CancellationToken ct)
        {
            var state = await _save.GetModuleAsync<SavedAttention>(AttentionSaveKeys.State, ct);
            return state ?? new SavedAttention();
        }

        public UniTask SaveAsync(SavedAttention state, CancellationToken ct)
            => _save.UpdateModuleAsync(
                AttentionSaveKeys.State,
                state ?? new SavedAttention(),
                AttentionSaveKeys.StateSchemaVersion,
                ct);
    }
}
