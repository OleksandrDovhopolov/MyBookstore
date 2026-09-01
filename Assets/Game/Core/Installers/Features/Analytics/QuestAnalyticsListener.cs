using System;
using System.Collections.Generic;
using Game.Quest.API;
using VContainer.Unity;

namespace Game.Bootstrap.Analytics
{
    public sealed class QuestAnalyticsListener : IStartable, IDisposable
    {
        private readonly IQuestsService _quests;
        private readonly global::Analytics.IAnalyticsService _analytics;

        public QuestAnalyticsListener(IQuestsService quests, global::Analytics.IAnalyticsService analytics)
        {
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Start()
        {
            _quests.QuestStarted += OnQuestStarted;
            _quests.QuestCompleted += OnQuestCompleted;
        }

        public void Dispose()
        {
            _quests.QuestStarted -= OnQuestStarted;
            _quests.QuestCompleted -= OnQuestCompleted;
        }

        private void OnQuestStarted(IQuest quest)
            => Track(global::Analytics.AnalyticsEventNames.QuestStarted, quest);

        private void OnQuestCompleted(IQuest quest)
            => Track(global::Analytics.AnalyticsEventNames.QuestCompleted, quest);

        private void Track(string eventName, IQuest quest)
        {
            if (quest == null) return;

            var parameters = new Dictionary<string, object>();
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.QuestId, quest.Id);
            AnalyticsParameterBag.AddString(parameters, global::Analytics.AnalyticsParameterNames.ChainId, quest.ChainId);

            _analytics.TrackEvent(new global::Analytics.AnalyticsEvent(eventName, parameters));
        }
    }
}
