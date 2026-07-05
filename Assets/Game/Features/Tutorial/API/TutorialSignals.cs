namespace Game.Tutorial.API
{
    /// <summary>Published when a tutorial sequence starts running.</summary>
    public readonly struct TutorialSequenceStarted
    {
        public string SequenceId { get; }

        public TutorialSequenceStarted(string sequenceId) => SequenceId = sequenceId;
    }

    /// <summary>Published when the active sequence advances to a step.</summary>
    public readonly struct TutorialStepChanged
    {
        public string SequenceId { get; }
        public string StepId { get; }
        public int StepIndex { get; }

        public TutorialStepChanged(string sequenceId, string stepId, int stepIndex)
        {
            SequenceId = sequenceId;
            StepId = stepId;
            StepIndex = stepIndex;
        }
    }

    /// <summary>Published when a sequence finishes (one-way completion).</summary>
    public readonly struct TutorialSequenceCompleted
    {
        public string SequenceId { get; }

        public TutorialSequenceCompleted(string sequenceId) => SequenceId = sequenceId;
    }
}
