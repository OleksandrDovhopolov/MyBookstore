using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Game.Quest.Services.Persistence
{
    /// <summary>
    /// MVP repository — local-only, backed by <see cref="ISaveService"/> module
    /// <see cref="QuestsSaveKeys.State"/>. Mirrors <c>SaveBackedSalesStatsRepository</c>.
    /// </summary>
    public sealed class SaveBackedQuestsRepository : IQuestsRepository
    {
        private readonly ISaveService _save;

        public SaveBackedQuestsRepository(ISaveService save)
            => _save = save ?? throw new ArgumentNullException(nameof(save));

        public async UniTask<SavedQuests> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<SavedQuests>(QuestsSaveKeys.State, ct);
            return dto ?? new SavedQuests();
        }

        public UniTask SaveAsync(SavedQuests state, CancellationToken ct)
            => _save.UpdateModuleAsync(QuestsSaveKeys.State, state ?? new SavedQuests(),
                QuestsSaveKeys.StateSchemaVersion, ct);
    }
}
