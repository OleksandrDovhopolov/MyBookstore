using System;
using Book.Sell.API;
using Book.Sell.UI;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.UI;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Debug trigger for the dialogue flow (GAME-6 §Этап 5, A6): one button per <see cref="DialogueConfig"/>
    /// that opens <see cref="DialogWindow"/> with a null controller. That makes Part A self-demoable without a
    /// sales scene or a real spawn — and it is safe: a null controller means no interaction lock is held, so
    /// there is nothing to hang. The window resolves the graph through the global <see cref="IConfigsService"/>.
    /// </summary>
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
                // controller: null → no sim/lock, the window just renders the graph and closes (§A6).
                await _uiManager.ShowAsync<DialogWindow>(
                    new DialogWindowArgs(controller: null, payload: new DialoguePayload(dialogueId)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} Failed to open dialogue '{dialogueId}': {ex}");
            }
        }
    }
}
