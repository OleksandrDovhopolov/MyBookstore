using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Newspaper.UI;
using Game.UI;
using VContainer;

namespace Game.Decor.UI
{
    /// <summary>
    /// Read-only decor info popup (<see cref="WindowType.Popup"/>), shown additively over
    /// <see cref="DecorPlacementWindow"/>. Opened with <see cref="DecorInfoPopupArgs"/>; does not
    /// touch placement or the placement window's selection.
    /// </summary>
    [Window("DecorInfoPopup", WindowType.Popup)]
    public sealed class DecorInfoPopup : WindowController<DecorInfoPopupView>
    {
        private IConfigsService _configs;
        private IUiSpriteProvider _sprites;
        private CancellationTokenSource _iconCts;

        [Inject]
        public void InjectServices(IConfigsService configs, IUiSpriteProvider sprites)
        {
            _configs = configs;
            _sprites = sprites;
        }

        protected override void OnInit()
        {
            if (View.CloseButton != null) View.CloseButton.onClick.AddListener(OnCloseClicked);
        }

        protected override void OnShowStart() => Apply();

        protected override void UpdateWindow() => Apply();

        protected override void OnHideStart(bool isClosed) => CancelIcon();

        protected override void OnDispose()
        {
            CancelIcon();
            if (View != null && View.CloseButton != null) View.CloseButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void Apply()
        {
            if (Arguments is not DecorInfoPopupArgs args) return;
            var config = _configs.Get<DecorConfig>(args.DecorId);
            if (config == null) return;

            if (View.NameLabel != null) View.NameLabel.text = config.DisplayName ?? config.Id;
            if (View.BonusesLabel != null)
                View.BonusesLabel.text = DecorSlotRowView.FormatEffects(config, View.PositiveColor, View.NegativeColor);
            if (View.DescriptionLabel != null) View.DescriptionLabel.text = BuildDescription(config);

            if (View.Icon != null)
            {
                View.Icon.sprite = null;
                LoadIconAsync(args.DecorId).Forget();
            }
        }

        private async UniTaskVoid LoadIconAsync(string decorId)
        {
            if (_sprites == null || View == null || View.Icon == null) return;

            CancelIcon();
            _iconCts = new CancellationTokenSource();
            var ct = _iconCts.Token;
            try
            {
                var sprite = await _sprites.GetSpriteAsync(decorId, ct);
                if (ct.IsCancellationRequested) return;
                if (View != null && View.Icon != null) View.Icon.sprite = sprite;
            }
            catch (System.OperationCanceledException) { }
        }

        private void CancelIcon()
        {
            if (_iconCts == null) return;
            _iconCts.Cancel();
            _iconCts.Dispose();
            _iconCts = null;
        }

        private static string BuildDescription(DecorConfig config)
        {
            var sb = new StringBuilder();
            sb.Append($"{config.PositionType} · {config.Size} · {config.Rarity}");
            if (config.AtmosphereTags != null && config.AtmosphereTags.Length > 0)
                sb.Append('\n').Append(string.Join(", ", config.AtmosphereTags));
            return sb.ToString();
        }

        private void OnCloseClicked() => CloseAsync().Forget();
    }
}
