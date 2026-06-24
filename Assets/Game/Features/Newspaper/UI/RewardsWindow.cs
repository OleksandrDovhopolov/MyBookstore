using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        private IUiSpriteProvider _uiSprites;
        private CancellationTokenSource _cts;

        [Inject]
        public void InjectServices(IConfigsService configs, IUiSpriteProvider uiSprites)
        {
            _configs = configs;
            _uiSprites = uiSprites;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();
        }

        protected override void OnShowStart() => ApplyArgsToView();

        protected override void UpdateWindow() => ApplyArgsToView();

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void ApplyArgsToView()
        {
            View.ResetView();
            if (Arguments is not RewardsWindowArgs args) return;

            // Cancel any in-flight icon load from a previous Apply (OnShowStart/UpdateWindow).
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var rewards = RewardsWindowRewardBuilder.Build(args.Granted, _configs, _ => null);
            View.SetReward(rewards);
            LoadRewardIconsAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid LoadRewardIconsAsync(CancellationToken ct)
        {
            if (View == null || _uiSprites == null) return;

            // Snapshot: the view's dictionary is rebuilt by ResetView/SetReward, so avoid iterating
            // the live collection across awaits.
            var entries = View.GetViews().ToList();

            try
            {
                foreach (var pair in entries)
                {
                    var resource = pair.Key;
                    var view = pair.Value;
                    if (resource == null || view == null) continue;

                    var sprite = await _uiSprites.GetSpriteAsync(resource.ResourceId, ct);
                    if (ct.IsCancellationRequested) return;
                    if (view != null) view.SetIcon(sprite);
                }
            }
            catch (OperationCanceledException)
            {
                // window closed / re-applied mid-load — ok
            }
        }
    }
}
