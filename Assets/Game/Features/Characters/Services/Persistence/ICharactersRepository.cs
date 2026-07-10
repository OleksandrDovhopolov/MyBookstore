using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Characters.Services.Persistence
{
    /// <summary>Persistence seam for character state. Mirrors <c>IQuestsRepository</c>.</summary>
    public interface ICharactersRepository
    {
        UniTask<SavedCharacters> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(SavedCharacters state, CancellationToken ct);
    }
}
