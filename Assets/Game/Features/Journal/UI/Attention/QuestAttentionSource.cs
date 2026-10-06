using System;
using System.Collections.Generic;
using Game.Attention.API;
using Game.Quest.API;

namespace Game.Journal.UI
{
    /// <summary>
    /// Ids of every quest currently sitting in one <see cref="QuestState"/>. Split into two concrete
    /// keys (see <see cref="JournalAttentionKeys"/>) because "new" and "ready to award" carry separate
    /// seen-sets — a quest the player has already noticed becoming active is still news when it
    /// becomes claimable.
    /// </summary>
    public abstract class QuestAttentionSource : IAttentionSource, IDisposable
    {
        private readonly IQuestsService _quests;
        private readonly QuestState _state;

        protected QuestAttentionSource(IQuestsService quests, QuestState state)
        {
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _state = state;

            _quests.QuestStarted += OnQuestChanged;
            _quests.QuestCompleted += OnQuestChanged;
            _quests.QuestAwarded += OnQuestChanged;
            _quests.QuestFailed += OnQuestChanged;
        }

        public abstract string Key { get; }

        public IEnumerable<string> CurrentIds
        {
            get
            {
                var quests = _quests.GetAllQuests();
                if (quests == null) yield break;

                for (var i = 0; i < quests.Count; i++)
                {
                    var quest = quests[i];
                    if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                    if (quest.Config?.HiddenInJournal == true) continue;   // service quest: no badge
                    if (quest.State == _state)
                        yield return quest.Id;
                }
            }
        }

        public event Action Changed;

        public void Dispose()
        {
            _quests.QuestStarted -= OnQuestChanged;
            _quests.QuestCompleted -= OnQuestChanged;
            _quests.QuestAwarded -= OnQuestChanged;
            _quests.QuestFailed -= OnQuestChanged;
        }

        private void OnQuestChanged(IQuest _) => Changed?.Invoke();
    }

    public sealed class QuestNewAttentionSource : QuestAttentionSource
    {
        public QuestNewAttentionSource(IQuestsService quests) : base(quests, QuestState.Active) { }

        public override string Key => JournalAttentionKeys.QuestsNew;
    }

    public sealed class QuestAwardAttentionSource : QuestAttentionSource
    {
        public QuestAwardAttentionSource(IQuestsService quests) : base(quests, QuestState.ReadyToAward) { }

        public override string Key => JournalAttentionKeys.QuestsAward;
    }
}
