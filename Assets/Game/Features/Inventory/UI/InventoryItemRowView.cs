using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
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
            Decor,
            QuestItem
        }

        [SerializeField] private GameObject _defaultRoot;
        [SerializeField] private GameObject _decorRoot;
        [SerializeField] private GameObject _questItemRoot;
        [SerializeField] private Image _defaultImage;
        [SerializeField] private Image _decorImage;
        [SerializeField] private Image _questItemImage;
        [SerializeField] private GameObject _decorPlacedRoot;
        [SerializeField] private TextMeshProUGUI _amountText;
        [SerializeField] private Button _infoButton;

        private Action<string, InventoryRowStyle, RectTransform> _onInfo;
        private string _itemId;
        private CancellationTokenSource _iconCts;
        private VisualMode _visualMode;
        private InventoryRowStyle _style;

        private void Awake()
        {
            if (_infoButton != null) _infoButton.onClick.AddListener(OnInfoClicked);
        }

        public void Bind(
            InventoryRowModel model,
            IUiSpriteProvider sprites,
            Action<string, InventoryRowStyle, RectTransform> onInfo,
            CancellationToken ct)
        {
            CancelIconLoad();
            _itemId = model.ItemId;
            _style = model.Style;
            _onInfo = !string.IsNullOrEmpty(_itemId) ? onInfo : null;

            SetVisualMode(ToVisualMode(model.Style));
            SetDecorPlacedVisible(model.IsHighlighted);
            if (_amountText != null)
                _amountText.text = model.Count > 0 ? model.Count.ToString() : string.Empty;
            SetInfoVisible(!string.IsNullOrEmpty(_itemId));

            if (sprites == null || string.IsNullOrEmpty(model.SpriteId)) return;
            LoadIconAsync(model.SpriteId, sprites, ct).Forget();
        }

        public void Cleanup()
        {
            CancelIconLoad();
            _onInfo = null;
            _itemId = null;
            _style = InventoryRowStyle.Default;
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
            if (string.IsNullOrEmpty(_itemId)) return;

            var anchor = _infoButton != null && _infoButton.transform is RectTransform buttonRect
                ? buttonRect
                : transform as RectTransform;
            _onInfo?.Invoke(_itemId, _style, anchor);
        }

        private static VisualMode ToVisualMode(InventoryRowStyle style)
        {
            return style switch
            {
                InventoryRowStyle.Decor => VisualMode.Decor,
                InventoryRowStyle.QuestItem => VisualMode.QuestItem,
                _ => VisualMode.Default
            };
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
            var questItemImage = GetQuestItemImage();

            SetImageSprite(defaultImage, null);
            if (decorImage != defaultImage) SetImageSprite(decorImage, null);
            if (questItemImage != defaultImage && questItemImage != decorImage) SetImageSprite(questItemImage, null);

            var defaultRoot = GetDefaultRoot();
            var decorRoot = GetDecorRoot();
            var questItemRoot = GetQuestItemRoot();

            SetRootActive(defaultRoot, IsRootActive(defaultRoot, mode, defaultRoot, decorRoot, questItemRoot));
            if (decorRoot != defaultRoot)
                SetRootActive(decorRoot, IsRootActive(decorRoot, mode, defaultRoot, decorRoot, questItemRoot));
            if (questItemRoot != defaultRoot && questItemRoot != decorRoot)
                SetRootActive(questItemRoot, IsRootActive(questItemRoot, mode, defaultRoot, decorRoot, questItemRoot));
        }

        private Image GetActiveImage()
        {
            return _visualMode switch
            {
                VisualMode.Default => GetDefaultImage(),
                VisualMode.Decor => GetDecorImage(),
                VisualMode.QuestItem => GetQuestItemImage(),
                _ => null
            };
        }

        private Image GetDefaultImage()
        {
            return _defaultImage;
        }

        private Image GetDecorImage()
        {
            return _decorImage != null ? _decorImage : _defaultImage;
        }

        private Image GetQuestItemImage()
        {
            return _questItemImage != null ? _questItemImage : _defaultImage;
        }

        private GameObject GetDefaultRoot()
        {
            return _defaultRoot;
        }

        private GameObject GetDecorRoot()
        {
            return _decorRoot != null ? _decorRoot : _defaultRoot;
        }

        private GameObject GetQuestItemRoot()
        {
            return _questItemRoot != null ? _questItemRoot : _defaultRoot;
        }

        private void SetImageSprite(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;

            // Source art has mixed sizes and aspects, so the rect has to be re-fitted per sprite.
            if (sprite != null && image.TryGetComponent<InventoryIconFitter>(out var fitter))
                fitter.Fit();
        }

        private void SetRootActive(GameObject root, bool visible)
        {
            if (root == null || root == gameObject) return;
            root.SetActive(visible);
        }

        private static bool IsRootActive(
            GameObject root,
            VisualMode mode,
            GameObject defaultRoot,
            GameObject decorRoot,
            GameObject questItemRoot)
        {
            if (root == null || mode == VisualMode.None) return false;
            return mode switch
            {
                VisualMode.Default => root == defaultRoot,
                VisualMode.Decor => root == decorRoot,
                VisualMode.QuestItem => root == questItemRoot,
                _ => false
            };
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
