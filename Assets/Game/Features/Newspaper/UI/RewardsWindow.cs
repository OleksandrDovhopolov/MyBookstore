using Game.Configs;
using Game.UI;
using VContainer;

namespace Game.Newspaper.UI
{
    //TODO should remove this from Game.Newspaper.UI and move closer to its domain
    /// <summary>
    /// Popup that summarises what a player received from a purchase.
    /// </summary>
    [Window("RewardsWindow", WindowType.Popup)]
    public sealed class RewardsWindow : WindowController<RewardWindowView>
    {
        private IConfigsService _configs;

        [Inject]
        public void InjectServices(IConfigsService configs)
        {
            _configs = configs;
        }

        protected override void OnInit()
        {
        }

        protected override void OnShowStart() => ApplyArgsToView();

        protected override void UpdateWindow() => ApplyArgsToView();

        protected override void OnDispose()
        {
        }

        private void ApplyArgsToView()
        {
            View.ResetView();
            if (Arguments is not RewardsWindowArgs args) return;

            var rewards = RewardsWindowRewardBuilder.Build(args.Granted, _configs, View.GetIconForReward);
            View.SetReward(rewards);
        }
    }
}
