namespace Game.Tutorial.API
{
    /// <summary>
    /// Pull-based re-evaluation seam for domain systems that can make tutorial eligibility change.
    /// </summary>
    public interface ITutorialReevaluationGate
    {
        void RequestReevaluation();
    }
}
