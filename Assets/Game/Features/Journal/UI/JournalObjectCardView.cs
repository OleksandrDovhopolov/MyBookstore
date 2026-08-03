using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SpriteService;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Journal.UI
{
    public sealed class JournalObjectCardView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Sprite _fallbackSprite;
        [SerializeField] private Button _infoButton;

        private string _decorId;
        private Action<string> _onInfoClicked;
        private CancellationTokenSource _iconCts;

        private void Awake()
        {
            if (_infoButton != null) _infoButton.onClick.AddListener(OnInfoClicked);
        }

        public void Bind(JournalObjectItemModel model, IUiSpriteProvider sprites, Action<string> onInfoClicked)
        {
            _decorId = model.DecorId;
            _onInfoClicked = onInfoClicked;
            CancelIconLoad();
            SetIcon(_fallbackSprite);
            if (sprites == null || string.IsNullOrEmpty(_decorId)) return;

            _iconCts = new CancellationTokenSource();
            LoadIconAsync(_decorId, sprites, _iconCts.Token).Forget();
        }

        private async UniTaskVoid LoadIconAsync(string decorId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(decorId, ct);
                if (ct.IsCancellationRequested) return;
                SetIcon(sprite != null ? sprite : _fallbackSprite);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Cleanup()
        {
            CancelIconLoad();
            _decorId = null;
            _onInfoClicked = null;
            SetIcon(null);
        }

        private void OnInfoClicked()
        {
            if (string.IsNullOrEmpty(_decorId)) return;
            _onInfoClicked?.Invoke(_decorId);
        }

        private void CancelIconLoad()
        {
            if (_iconCts == null) return;
            _iconCts.Cancel();
            _iconCts.Dispose();
            _iconCts = null;
        }

        private void SetIcon(Sprite sprite)
        {
            if (_icon == null) return;
            _icon.sprite = sprite;
        }

        private void OnDestroy()
        {
            CancelIconLoad();
            if (_infoButton != null) _infoButton.onClick.RemoveListener(OnInfoClicked);
        }
    }
}
