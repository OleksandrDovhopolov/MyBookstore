using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SpriteService;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Journal.UI
{
    public sealed class JournalBonusRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Sprite _fallbackSprite;
        [SerializeField] private TextMeshProUGUI _percentLabel;
        [SerializeField] private Color _positiveColor = Color.green;
        [SerializeField] private Color _negativeColor = Color.red;

        private CancellationTokenSource _iconCts;

        public void Bind(JournalBonusItemModel model, IUiSpriteProvider sprites)
        {
            if (_percentLabel != null)
            {
                _percentLabel.text = model.Label;
                _percentLabel.color = model.IsPositive ? _positiveColor : _negativeColor;
            }

            CancelIconLoad();
            SetIcon(_fallbackSprite);

            if (sprites == null || string.IsNullOrEmpty(model.IconKey)) return;

            _iconCts = new CancellationTokenSource();
            LoadIconAsync(model.IconKey, sprites, _iconCts.Token).Forget();
        }

        private async UniTaskVoid LoadIconAsync(string iconKey, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(iconKey, ct);
                if (ct.IsCancellationRequested) return;
                if (_icon == null) return;
                SetIcon(sprite != null ? sprite : _fallbackSprite);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Cleanup()
        {
            CancelIconLoad();
            SetIcon(null);

            if (_percentLabel != null) _percentLabel.text = string.Empty;
        }

        private void SetIcon(Sprite sprite)
        {
            if (_icon == null) return;
            _icon.sprite = sprite;
            _icon.gameObject.SetActive(sprite != null);
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
