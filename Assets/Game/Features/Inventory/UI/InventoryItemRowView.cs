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
        private enum VisualMode
        {
            None,
            Default,
            Decor
        }

        [SerializeField] private GameObject _defaultRoot;
        [SerializeField] private GameObject _decorRoot;
        [SerializeField] private Image _defaultImage;
        [SerializeField] private Image _decorImage;
        [SerializeField] private GameObject _decorPlacedRoot;
        [SerializeField] private TextMeshProUGUI _amountText;
        [SerializeField] private Button _infoButton;

        private Action<string> _onInfo;
        private string _decorId;
        private CancellationTokenSource _iconCts;
        private VisualMode _visualMode;

        private void Awake()
        {
            if (_infoButton != null) _infoButton.onClick.AddListener(OnInfoClicked);
        }

        public void BindGenre(BookGenre genre, int count, IUiSpriteProvider sprites, CancellationToken ct)
        {
            CancelIconLoad();
            _onInfo = null;
            _decorId = null;

            SetVisualMode(VisualMode.Default);
            SetDecorPlacedVisible(false);
            if (_amountText != null)
                _amountText.text = count.ToString();
            SetInfoVisible(false);

            var genreId = genre.ToConfigValue();
            if (sprites == null || string.IsNullOrEmpty(genreId)) return;
            LoadIconAsync(genreId, sprites, ct).Forget();
        }

        public void BindDecor(DecorConfig config, bool isPlaced, IUiSpriteProvider sprites, Action<string> onInfo, CancellationToken ct)
        {
            CancelIconLoad();
            _decorId = config.Id;
            _onInfo = onInfo;

            SetVisualMode(VisualMode.Decor);
            SetDecorPlacedVisible(isPlaced);
            if (_amountText != null) _amountText.text = string.Empty;
            SetInfoVisible(true);

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
            SetVisualMode(VisualMode.None);
            SetDecorPlacedVisible(false);
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
            SetImageSprite(GetActiveImage(), sprite);
        }

        private void SetVisualMode(VisualMode mode)
        {
            _visualMode = mode;

            var defaultImage = GetDefaultImage();
            var decorImage = GetDecorImage();

            SetImageSprite(defaultImage, null);
            if (decorImage != defaultImage) SetImageSprite(decorImage, null);

            var defaultRoot = GetDefaultRoot();
            var decorRoot = GetDecorRoot();

            if (defaultRoot == decorRoot)
            {
                SetRootActive(defaultRoot, mode != VisualMode.None);
            }
            else
            {
                SetRootActive(defaultRoot, mode == VisualMode.Default);
                SetRootActive(decorRoot, mode == VisualMode.Decor);
            }
        }

        private Image GetActiveImage()
        {
            return _visualMode switch
            {
                VisualMode.Default => GetDefaultImage(),
                VisualMode.Decor => GetDecorImage(),
                _ => null
            };
        }

        private Image GetDefaultImage()
        {
            return _defaultImage;
        }

        private Image GetDecorImage()
        {
            return _decorImage;
        }

        private GameObject GetDefaultRoot()
        {
            return _defaultRoot;
        }

        private GameObject GetDecorRoot()
        {
            return _decorRoot;
        }

        private void SetImageSprite(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void SetRootActive(GameObject root, bool visible)
        {
            if (root == null || root == gameObject) return;
            root.SetActive(visible);
        }

        private void SetDecorPlacedVisible(bool visible)
        {
            if (_decorPlacedRoot == null || _decorPlacedRoot == gameObject) return;
            _decorPlacedRoot.SetActive(visible);
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
