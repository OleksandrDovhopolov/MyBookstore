using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Quest.API;

namespace Book.Sell.UI
{
    /// <summary>Activates a quest authored on a dialogue config, if any.</summary>
    public sealed class DialogueQuestActivator
    {
        private readonly IConfigsService _configs;
        private readonly IQuestsService _quests;

        public DialogueQuestActivator(IConfigsService configs = null, IQuestsService quests = null)
        {
            _configs = configs;
            _quests = quests;
        }

        public async UniTask<bool> ActivateForDialogueAsync(string dialogueId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(dialogueId) || _configs == null || _quests == null)
                return false;

            if (!_configs.TryGet<DialogueConfig>(dialogueId, out var config) || config == null)
                return false;

            var questId = config.ActivatesQuestId;
            if (string.IsNullOrWhiteSpace(questId)) return false;

            return await _quests.TryActivateAsync(questId, ct);
        }
    }
}
