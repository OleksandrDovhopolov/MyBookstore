using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Tutorial.API
{
    public enum TutorialContext
    {
        Any = 0,
        Hub,
        Location
    }

    public enum TutorialResumePolicy
    {
        Restart = 0,
        FromStep
    }

    public enum TutorialTrigger
    {
        HubReady = 0,
        LocationLoaded,
        PhaseChanged,
        QuestStarted,
        QuestCompleted
    }

    public interface ITutorialStep
    {
        string Id { get; }

        UniTask ExecuteAsync(CancellationToken ct);
    }

    public interface ITutorialSequence
    {
        string Id { get; }
        int Priority { get; }
        TutorialContext Context { get; }
        TutorialTrigger Trigger { get; }
        string TriggerParam { get; }
        TutorialResumePolicy ResumePolicy { get; }

        bool IsEligible();
        IReadOnlyList<ITutorialStep> GetSteps();
    }
}
