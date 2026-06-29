using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Game.Characters.Services.Persistence
{
    /// <summary>
    /// MVP repository — local-only, backed by <see cref="ISaveService"/> module
    /// <see cref="CharactersSaveKeys.State"/>. Mirrors <c>SaveBackedQuestsRepository</c>.
    /// </summary>
    public sealed class SaveBackedCharactersRepository : ICharactersRepository
    {
        private readonly ISaveService _save;

        public SaveBackedCharactersRepository(ISaveService save)
            => _save = save ?? throw new ArgumentNullException(nameof(save));

        public async UniTask<SavedCharacters> LoadAsync(CancellationToken ct)
        {
            var dto = await _save.GetModuleAsync<SavedCharacters>(CharactersSaveKeys.State, ct);
            return dto ?? new SavedCharacters();
        }

        public UniTask SaveAsync(SavedCharacters state, CancellationToken ct)
            => _save.UpdateModuleAsync(CharactersSaveKeys.State, state ?? new SavedCharacters(),
                CharactersSaveKeys.StateSchemaVersion, ct);
    }
}
