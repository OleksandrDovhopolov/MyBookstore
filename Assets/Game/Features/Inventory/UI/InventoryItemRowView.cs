using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using SpriteService;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory.UI
{
    public sealed class InventoryItemRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _image;
        [SerializeField] private TextMeshProUGUI _amountText;
        [SerializeField] private Button _infoButton;

        private Action<string> _onInfo;
        private string _decorId;
        private CancellationTokenSource _iconCts;

        private void Awake()
        {
            if (_infoButton != null) _infoButton.onClick.AddListener(OnInfoClicked);
        }

        public void BindGenre(BookGenre genre, int count, IUiSpriteProvider sprites, CancellationToken ct)
        {
            _onInfo = null;
            _decorId = null;

            if (_amountText != null)
                _amountText.text = count.ToString();
            SetInfoVisible(false);

            SetIcon(null);

            var genreId = genre.ToConfigValue();
            if (sprites == null || string.IsNullOrEmpty(genreId)) return;
            LoadIconAsync(genreId, sprites, ct).Forget();
        }

        public void BindDecor(DecorConfig config, IUiSpriteProvider sprites, Action<string> onInfo, CancellationToken ct)
        {
            _decorId = config.Id;
            _onInfo = onInfo;

            if (_amountText != null)
                _amountText.text = "1";
            SetInfoVisible(true);

            SetIcon(null);

            if (sprites == null || string.IsNullOrEmpty(config.Id)) return;
            LoadIconAsync(config.Id, sprites, ct).Forget();
        }

        public void Cleanup()
        {
            CancelIconLoad();
            _onInfo = null;
            _decorId = null;
            if (_amountText != null) _amountText.text = string.Empty;
            SetInfoVisible(false);
            SetIcon(null);
        }

        private async UniTaskVoid LoadIconAsync(string spriteId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            CancelIconLoad();
            _iconCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var linkedCt = _iconCts.Token;

            try
            {
                var sprite = await sprites.GetSpriteAsync(spriteId, linkedCt);
                if (linkedCt.IsCancellationRequested) return;
                SetIcon(sprite);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryItemRowView] Failed to load sprite '{spriteId}': {e.Message}");
            }
        }

        private void OnInfoClicked()
        {
            if (!string.IsNullOrEmpty(_decorId)) _onInfo?.Invoke(_decorId);
        }

        private void SetInfoVisible(bool visible)
        {
            if (_infoButton == null) return;

            if (_infoButton.gameObject != gameObject)
                _infoButton.gameObject.SetActive(visible);

            _infoButton.interactable = visible;
        }

        private void SetIcon(Sprite sprite)
        {
            if (_image == null) return;
            _image.sprite = sprite;
            _image.enabled = sprite != null;
        }

        private void CancelIconLoad()
        {
            if (_iconCts == null) return;
            _iconCts.Cancel();
            _iconCts.Dispose();
            _iconCts = null;
        }

        private void OnDestroy()
        {
            CancelIconLoad();
            if (_infoButton != null) _infoButton.onClick.RemoveListener(OnInfoClicked);
        }
    }
}
