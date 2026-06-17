using System.Text;
using Cysharp.Threading.Tasks;
using Game.Rewards.API;
using Game.UI;

namespace Game.Newspaper.UI
{
    //TODO this class should be in Newspaper ? 
    /// <summary>
    /// Popup that summarises what a player received from a purchase. Phase 1 MVP: textual list
    /// (item id × amount per line). Phase 2+: icons, animations, claim flow per spec.
    /// </summary>
    [Window("RewardsWindow", WindowType.Popup)]
    public sealed class RewardsWindow : WindowController<RewardsWindowView>
    {
        protected override void OnInit()
        {
            View.OkButton.onClick.AddListener(OnOkClicked);
        }

        protected override void OnShowStart() => ApplyArgsToView();

        protected override void UpdateWindow() => ApplyArgsToView();

        protected override void OnDispose()
        {
            if (View != null)
                View.OkButton.onClick.RemoveListener(OnOkClicked);
        }

        private void ApplyArgsToView()
        {
            if (Arguments is not RewardsWindowArgs args) return;

            if (View.TitleLabel != null)
                View.TitleLabel.text = args.Title;

            if (View.BodyLabel != null)
                View.BodyLabel.text = BuildBody(args.Granted);
        }

        private static string BuildBody(RewardSpec granted)
        {
            if (granted == null || granted.Items == null || granted.Items.Count == 0)
                return "Nothing received.";

            var sb = new StringBuilder();
            for (var i = 0; i < granted.Items.Count; i++)
            {
                var item = granted.Items[i];
                sb.Append("• ");
                sb.Append(item.Id);
                if (item.Amount > 1)
                {
                    sb.Append(" ×");
                    sb.Append(item.Amount);
                }
                if (i < granted.Items.Count - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        private void OnOkClicked() => CloseAsync().Forget();
    }
}
