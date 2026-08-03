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
        private const string CharacterId = "owner";
        private const string MovingInMemoryId = "mem_owner_moving_in";
        private const string PlaceholderMemoryId = "mem_owner_placeholder";

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
            AddUnlockButton(cheatsContainer, "Unlock owner moving in", MovingInMemoryId);
            AddUnlockButton(cheatsContainer, "Unlock owner placeholder", PlaceholderMemoryId);
        }

        private void AddUnlockButton(ICheatsContainer cheatsContainer, string label, string memoryId)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick(label, () => UnlockAsync(memoryId).Forget())
                    .WithGroup(Group));
        }

        private async UniTaskVoid UnlockAsync(string memoryId)
        {
            try
            {
                var unlocked = _characters.TryUnlockMemory(CharacterId, memoryId);
                if (!unlocked)
                {
                    Debug.Log($"{LogTag} '{CharacterId}.{memoryId}' is already unlocked or missing.");
                    return;
                }

                await _save.SaveAsync(_ct);
                Debug.Log($"{LogTag} unlocked '{CharacterId}.{memoryId}'.");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
