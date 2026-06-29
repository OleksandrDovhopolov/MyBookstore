using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Quest.Services.Persistence
{
    /// <summary>
    /// Persistence seam under <see cref="QuestsService"/>. MVP is local (save-module backed); a server
    /// implementation can swap in via DI later. Mirrors <c>ISalesStatsRepository</c>.
    /// </summary>
    public interface IQuestsRepository
    {
        UniTask<SavedQuests> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(SavedQuests state, CancellationToken ct);
    }
}
