using System.Collections.Generic;
using Game.Quest.API;

namespace Game.Quest.Services
{
    /// <summary>
    /// Ordered, linear view over the quests of one chain. Built on demand by <see cref="QuestsService"/>.
    /// </summary>
    internal sealed class QuestChain : IQuestChain
    {
        public string Id { get; }
        public IReadOnlyList<IQuest> Quests { get; }

        public QuestChain(string id, IReadOnlyList<IQuest> quests)
        {
            Id = id;
            Quests = quests;
        }

        public IQuest CurrentQuest
        {
            get
            {
                IQuest firstPending = null;
                for (var i = 0; i < Quests.Count; i++)
                {
                    var q = Quests[i];
                    if (q.State == QuestState.Active) return q;
                    if (firstPending == null && q.State == QuestState.Pending) firstPending = q;
                }
                return firstPending ?? FinalQuest;
            }
        }

        public IQuest FinalQuest => Quests.Count > 0 ? Quests[Quests.Count - 1] : null;
    }
}
