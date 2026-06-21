namespace Book.Sell.UI
{
    /// <summary>
    /// Tracks the active recommendation minigame window so <see cref="SalesScreenView"/> can pause the day
    /// while it is open. Implemented by <see cref="RecommendationMinigamePresenter"/>.
    /// </summary>
    public interface IRecommendationMinigamePresenter
    {
        bool IsWindowOpen { get; }
    }
}
