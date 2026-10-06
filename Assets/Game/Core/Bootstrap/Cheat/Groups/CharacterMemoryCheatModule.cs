using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Characters.API;
using Save;
using UnityEngine;

namespace Game.Cheat
{
    public sealed class CharacterMemoryCheatModule : ICheatsModule
    {
        private const string Group = "Characters";
        private const string LogTag = "[CharacterMemoryCheat]";

        private readonly ICharactersService _characters;
        private readonly ISaveService _save;
        private readonly CancellationToken _ct;

        public CharacterMemoryCheatModule(ICharactersService characters, ISaveService save, CancellationToken ct)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("Unlock all memories", () => UnlockAllAsync().Forget())
                    .WithGroup(Group));
        }

        /// <summary>
        /// Writes every memory into the unlock ledger, which is the OR-partner of the quest-derived state
        /// in <c>CharactersService.IsMemoryUnlocked</c> — so this works for quest-linked memories too
        /// without touching quest state. Idempotent: already-unlocked ids are skipped.
        ///
        /// There is deliberately no "clear all" counterpart: 7 of the 12 memories are derived from
        /// QuestState.Awarded, so wiping the ledger would neither hide them nor survive the next
        /// Reconcile(). Use Tools/Save/Reset Player Save for a real reset.
        /// </summary>
        private async UniTaskVoid UnlockAllAsync()
        {
            try
            {
                var unlocked = 0;
                var total = 0;

                foreach (var character in _characters.GetAllCharacters())
                {
                    var memories = character?.Memories;
                    if (memories == null) continue;

                    for (var i = 0; i < memories.Count; i++)
                    {
                        var memory = memories[i];
                        if (memory == null || string.IsNullOrEmpty(memory.Id)) continue;

                        total++;
                        if (_characters.TryUnlockMemory(character.Id, memory.Id)) unlocked++;
                    }
                }

                if (unlocked == 0)
                {
                    Debug.Log($"{LogTag} all {total} memories were already unlocked.");
                    return;
                }

                await _save.SaveAsync(_ct);
                Debug.Log($"{LogTag} unlocked {unlocked} of {total} memories.");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
