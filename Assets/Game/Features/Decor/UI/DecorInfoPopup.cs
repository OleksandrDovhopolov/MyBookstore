using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Newspaper.UI;
using Game.UI;
using UIShared;
using UnityEngine;
using VContainer;

namespace Game.Decor.UI
{
    [Window("DecorInfoPopup", WindowType.Popup)]
    public sealed class DecorInfoPopup : WindowController<DecorInfoPopupView>
    {
        // DecorConfig has no authored flavor description yet. Placeholder shown in _descriptionLabel
        // until a Description field exists on DecorConfig (then feed config.Description here instead).
        private const string DescriptionPlaceholder =
            "TODO: item description. Add a Description field to DecorConfig and pass it here.";

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
        }

        protected override void OnShowStart() => Apply();

        protected override void UpdateWindow() => Apply();

        protected override void OnHideStart(bool isClosed) => CancelIcon();

        protected override void OnDispose()
        {
            CancelIcon();
        }

        private void Apply()
        {
            if (Arguments is not DecorInfoPopupArgs args) return;
            var config = _configs.Get<DecorConfig>(args.DecorId);
            if (config == null) return;

            if (View.NameLabel != null) View.NameLabel.text = config.DisplayName ?? config.Id;
            if (View.DescriptionLabel != null) View.DescriptionLabel.text = DescriptionPlaceholder;

            RenderBonuses(config);
            RenderCharacteristics(config);

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

        // Bonuses: one pooled row per genre multiplier (icon placeholder + genre + signed percent).
        private void RenderBonuses(DecorConfig config)
        {
            var pool = View.BonusesPool;
            if (pool == null) return;

            pool.DisableAll();
            var mods = config.GenreMultipliers;
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    if (mod == null) continue;
                    var percent = Mathf.RoundToInt((mod.Multiplier - 1f) * 100f);
                    var sign = percent >= 0 ? "+" : "";
                    var color = mod.Multiplier < 1f ? View.NegativeColor : View.PositiveColor;
                    pool.GetNext().Bind(View.BonusIconPlaceholder, mod.Genre, $"{sign}{percent}%", color);
                }
            }
            pool.DisableNonActive();
        }

        // Characteristics: one pooled chip per trait (position, size, atmosphere tags).
        private void RenderCharacteristics(DecorConfig config)
        {
            var pool = View.CharacteristicsPool;
            if (pool == null) return;

            pool.DisableAll();
            var icon = View.CharacteristicIconPlaceholder;
            AddCharacteristic(pool, icon, config.PositionType.ToString());
            AddCharacteristic(pool, icon, config.Size.ToString());
            if (config.AtmosphereTags != null)
                foreach (var tag in config.AtmosphereTags)
                    if (!string.IsNullOrEmpty(tag))
                        AddCharacteristic(pool, icon, tag);
            pool.DisableNonActive();
        }

        private static void AddCharacteristic(UIListPool<DecorCharacteristicItemView> pool, Sprite icon, string text)
            => pool.GetNext().Bind(icon, text);
    }
}
