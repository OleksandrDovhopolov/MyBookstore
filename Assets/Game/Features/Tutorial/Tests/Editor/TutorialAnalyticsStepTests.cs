using Analytics;
using Game.Tutorial.API;
using Game.Tutorial.Content;
using NUnit.Framework;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialAnalyticsStepTests
    {
        [Test]
        public void Checkpoint_TracksTutorialCheckpointEvent()
        {
            var analytics = new RecordingAnalyticsService();
            var step = TutorialAnalyticsSteps.Checkpoint(
                "checkpoint_eddi_intro_start",
                analytics,
                TutorialSequenceIds.DayOne,
                TutorialContent.Analytics.EddiIntroStage,
                TutorialContent.Analytics.StateStart);

            step.ExecuteAsync(default).GetAwaiter().GetResult();

            Assert.AreEqual(1, analytics.Events.Count);
            var evt = analytics.Events[0];
            Assert.AreEqual(AnalyticsEventNames.TutorialCheckpoint, evt.Name);
            Assert.AreEqual(TutorialSequenceIds.DayOne, evt.Parameters[AnalyticsParameterNames.TutorialId]);
            Assert.AreEqual(TutorialContent.Analytics.EddiIntroStage, evt.Parameters[AnalyticsParameterNames.TutorialStage]);
            Assert.AreEqual("checkpoint_eddi_intro_start", evt.Parameters[AnalyticsParameterNames.TutorialStepId]);
            Assert.AreEqual(TutorialContent.Analytics.StateStart, evt.Parameters[AnalyticsParameterNames.TutorialState]);
        }
    }
}
