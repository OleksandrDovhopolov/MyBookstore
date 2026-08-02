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
    public sealed class JournalObjectCardView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameLabel;

        private CancellationTokenSource _iconCts;

        public void Bind(JournalObjectItemModel model, IUiSpriteProvider sprites)
        {
            if (_nameLabel != null) _nameLabel.text = model.DisplayName;
            CancelIconLoad();
            if (_icon != null) _icon.sprite = null;
            if (sprites == null || string.IsNullOrEmpty(model.DecorId)) return;

            _iconCts = new CancellationTokenSource();
            LoadIconAsync(model.DecorId, sprites, _iconCts.Token).Forget();
        }

        private async UniTaskVoid LoadIconAsync(string decorId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(decorId, ct);
                if (ct.IsCancellationRequested) return;
                if (_icon != null) _icon.sprite = sprite;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Cleanup()
        {
            CancelIconLoad();
            if (_icon != null) _icon.sprite = null;
            if (_nameLabel != null) _nameLabel.text = string.Empty;
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
