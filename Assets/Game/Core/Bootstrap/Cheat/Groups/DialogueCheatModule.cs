using System;
using cheatModule;
using Dialogue;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.UI;
using UnityEngine;

namespace Game.Cheat
{
    public class DialogueCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Dialogue";
        private const string LogTag = "[DialogueCheat]";

        private readonly IUIManager _uiManager;
        private readonly IConfigsService _configs;

        public DialogueCheatModule(IUIManager uiManager, IConfigsService configs)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            foreach (var cfg in _configs.GetAll<DialogueConfig>())
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                var id = cfg.Id;

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick(id, () => OpenDialogueAsync(id).Forget())
                        .WithGroup(CardsGroup));
            }
        }

        private async UniTaskVoid OpenDialogueAsync(string dialogueId)
        {
            try
            {
                // No completion callback → no sim/lock, the window just renders the graph and closes (§A6).
                await _uiManager.ShowAsync<DialogWindow>(
                    new DialogWindowArgs(new DialoguePayload(dialogueId)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} Failed to open dialogue '{dialogueId}': {ex}");
            }
        }
    }
}
