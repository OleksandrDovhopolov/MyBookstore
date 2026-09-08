using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Localization;
using SpriteService;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Journal.UI
{
    public sealed class JournalMemoryRowView : MonoBehaviour, ICleanup
    {
        private const float PhotoWidth = 342.1647f;
        private const float HalfPhotoWidth = PhotoWidth * 0.5f;

        [SerializeField] private Image _photoImage;
        [SerializeField] private Sprite _photoFallback;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;

        private CancellationTokenSource _photoCts;

        public void Bind(JournalMemoryItemModel model, IUiSpriteProvider sprites, bool imageLeft)
        {
            ApplyLayout(imageLeft);

            if (_titleLabel != null) _titleLabel.text = LocalizationLocator.GetOrKey(model.TitleKey);
            if (_descriptionLabel != null) _descriptionLabel.text = LocalizationLocator.GetOrKey(model.DescriptionKey);

            CancelPhotoLoad();
            SetPhoto(_photoFallback);
            if (sprites == null || string.IsNullOrEmpty(model.PhotoKey)) return;

            _photoCts = new CancellationTokenSource();
            LoadPhotoAsync(model.PhotoKey, sprites, _photoCts.Token).Forget();
        }

        private async UniTaskVoid LoadPhotoAsync(string photoKey, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(photoKey, ct);
                if (ct.IsCancellationRequested) return;
                SetPhoto(sprite != null ? sprite : _photoFallback);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Journal] failed to load memory photo '{photoKey}': {e.Message}");
                SetPhoto(_photoFallback);
            }
        }

        public void Cleanup()
        {
            CancelPhotoLoad();
            SetPhoto(null);
            if (_titleLabel != null) _titleLabel.text = string.Empty;
            if (_descriptionLabel != null) _descriptionLabel.text = string.Empty;
        }

        private void ApplyLayout(bool imageLeft)
        {
            ApplyPhotoLayout(imageLeft);
            ApplyDescriptionLayout(imageLeft);

            if (_titleLabel != null)
                _titleLabel.horizontalAlignment = imageLeft ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
        }

        private void ApplyPhotoLayout(bool imageLeft)
        {
            if (_photoImage == null) return;

            var rect = _photoImage.rectTransform;
            rect.anchorMin = new Vector2(imageLeft ? 0f : 1f, 0f);
            rect.anchorMax = new Vector2(imageLeft ? 0f : 1f, 1f);
            rect.anchoredPosition = new Vector2(imageLeft ? HalfPhotoWidth : -HalfPhotoWidth, 0f);
            rect.sizeDelta = new Vector2(PhotoWidth, 0f);
        }

        private void ApplyDescriptionLayout(bool imageLeft)
        {
            if (_descriptionLabel == null) return;

            var rect = _descriptionLabel.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(imageLeft ? HalfPhotoWidth : -HalfPhotoWidth, 0f);
            rect.sizeDelta = new Vector2(-PhotoWidth, 0f);
            _descriptionLabel.horizontalAlignment = HorizontalAlignmentOptions.Left;
        }

        private void SetPhoto(Sprite sprite)
        {
            if (_photoImage == null) return;
            _photoImage.sprite = sprite;
            _photoImage.enabled = sprite != null;
        }

        private void CancelPhotoLoad()
        {
            if (_photoCts == null) return;
            _photoCts.Cancel();
            _photoCts.Dispose();
            _photoCts = null;
        }

        private void OnDestroy() => CancelPhotoLoad();
    }
}
