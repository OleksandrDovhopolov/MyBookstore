using System.Collections.Generic;
using Analytics;
using Game.Tutorial.Content;
using NUnit.Framework;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialAnalyticsStepTests
    {
        [Test]
        public void Checkpoint_TracksTutorialCheckpointEvent()
        {
            var analytics = new FakeAnalyticsService();
            var step = TutorialAnalyticsSteps.Checkpoint(
                "checkpoint_eddi_intro_start",
                analytics,
                "tutorial_day_1",
                TutorialContent.Analytics.EddiIntroStage,
                TutorialContent.Analytics.StateStart);

            step.ExecuteAsync(default).GetAwaiter().GetResult();

            Assert.AreEqual(1, analytics.Events.Count);
            var evt = analytics.Events[0];
            Assert.AreEqual(AnalyticsEventNames.TutorialCheckpoint, evt.Name);
            Assert.AreEqual("tutorial_day_1", evt.Parameters[AnalyticsParameterNames.TutorialId]);
            Assert.AreEqual(TutorialContent.Analytics.EddiIntroStage, evt.Parameters[AnalyticsParameterNames.TutorialStage]);
            Assert.AreEqual("checkpoint_eddi_intro_start", evt.Parameters[AnalyticsParameterNames.TutorialStepId]);
            Assert.AreEqual(TutorialContent.Analytics.StateStart, evt.Parameters[AnalyticsParameterNames.TutorialState]);
        }

        private sealed class FakeAnalyticsService : IAnalyticsService
        {
            public List<IAnalyticsEvent> Events { get; } = new();
            public bool IsInitialized => true;
            public void Initialize() { }
            public void TrackEvent(IAnalyticsEvent analyticsEvent) => Events.Add(analyticsEvent);
            public void SetUserId(string userId) { }
            public void SetUserProperty(string key, string value) { }
            public void Flush() { }
        }
    }
}
