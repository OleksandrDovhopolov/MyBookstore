using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Localization;
using SpriteService;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Decor.UI
{
    /// <summary>
    /// One decor bonus row: genre book icon + composed description ("+30% Classic sale chance") +
    /// signed percent. Pooled via <see cref="UIListPool{T}"/> in <see cref="DecorInfoPopupView"/>.
    /// The icon is the genre's book sprite, loaded by genre id via <see cref="IUiSpriteProvider"/>.
    /// </summary>
    public sealed class DecorBonusItemView : MonoBehaviour, ICleanup
    {
        // Appended after "{percent} {genre}" to form the full bonus line.
        private const string DescriptionSuffixKey = "ui.decor.bonus.sale_chance";

        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private TextMeshProUGUI _percentLabel;

        private CancellationTokenSource _iconCts;

        public void Bind(string genre, string percent, Color percentColor, IUiSpriteProvider sprites)
        {
            Apply(genre, percent, percentColor);
            LoadIcon(genre, sprites);
        }

        public void Bind(Sprite icon, string genre, string percent, Color percentColor)
        {
            CancelIconLoad();
            if (_icon != null) _icon.sprite = icon;
            Apply(genre, percent, percentColor);
        }

        private void Apply(string genre, string percent, Color percentColor)
        {
            if (_descriptionLabel != null)
                _descriptionLabel.text = $"{percent} {genre} {LocalizationLocator.GetOrKey(DescriptionSuffixKey)}";
            if (_percentLabel != null)
            {
                _percentLabel.text = percent;
                _percentLabel.color = percentColor;
            }
        }

        private void LoadIcon(string genre, IUiSpriteProvider sprites)
        {
            CancelIconLoad();
            if (sprites == null || _icon == null || string.IsNullOrEmpty(genre)) return;

            _iconCts = new CancellationTokenSource();
            LoadIconAsync(genre, sprites, _iconCts.Token).Forget();
        }

        private async UniTaskVoid LoadIconAsync(string genre, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(genre, ct);
                if (ct.IsCancellationRequested) return;
                if (_icon != null) _icon.sprite = sprite;
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Called by UIListPool when the row is (re)acquired or disabled so a pooled instance never
        // shows stale data or an in-flight icon load.
        public void Cleanup()
        {
            CancelIconLoad();
            if (_icon != null) _icon.sprite = null;
            if (_descriptionLabel != null) _descriptionLabel.text = string.Empty;
            if (_percentLabel != null) _percentLabel.text = string.Empty;
        }

        private void CancelIconLoad()
        {
            if (_iconCts == null) return;
            _iconCts.Cancel();
            _iconCts.Dispose();
            _iconCts = null;
        }

        private void OnDestroy() => CancelIconLoad();
    }
}
