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
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _percentLabel;
        [SerializeField] private Color _positiveColor = Color.green;
        [SerializeField] private Color _negativeColor = Color.red;

        private CancellationTokenSource _iconCts;

        public void Bind(JournalBonusItemModel model, IUiSpriteProvider sprites)
        {
            if (_label != null) _label.text = model.Label;
            if (_percentLabel != null)
            {
                _percentLabel.text = model.PercentText;
                _percentLabel.color = model.IsPositive ? _positiveColor : _negativeColor;
            }

            CancelIconLoad();
            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.gameObject.SetActive(false);
            }

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
                _icon.sprite = sprite;
                _icon.gameObject.SetActive(sprite != null);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Cleanup()
        {
            CancelIconLoad();
            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.gameObject.SetActive(false);
            }

            if (_label != null) _label.text = string.Empty;
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
