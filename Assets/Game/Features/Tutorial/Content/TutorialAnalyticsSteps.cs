using System.Collections.Generic;
using Analytics;

namespace Game.Tutorial.Content
{
    public static class TutorialAnalyticsSteps
    {
        public static TutorialActionStep Checkpoint(
            string stepId,
            IAnalyticsService analytics,
            string tutorialId,
            string stage,
            string state)
        {
            return new TutorialActionStep(stepId, () =>
            {
                analytics?.TrackEvent(new AnalyticsEvent(
                    AnalyticsEventNames.TutorialCheckpoint,
                    new Dictionary<string, object>
                    {
                        [AnalyticsParameterNames.TutorialId] = tutorialId,
                        [AnalyticsParameterNames.TutorialStage] = stage,
                        [AnalyticsParameterNames.TutorialStepId] = stepId,
                        [AnalyticsParameterNames.TutorialState] = state,
                    }));
            });
        }
    }
}
