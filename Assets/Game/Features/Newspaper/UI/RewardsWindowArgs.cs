using Game.Rewards.API;
using Game.UI;

namespace Game.Newspaper.UI
{
    /// <summary>
    /// Args passed to <see cref="RewardsWindow"/>. Carries the spec actually granted (post-expansion)
    /// so the popup displays real outcomes — concrete book ids, decor names, etc.
    /// </summary>
    public sealed class RewardsWindowArgs : WindowArgs
    {
        public RewardSpec Granted { get; }
        public string Title { get; }

        public RewardsWindowArgs(RewardSpec granted, string title = "Reward")
        {
            Granted = granted;
            Title = title;
            AsAdditional();
        }
    }
}
