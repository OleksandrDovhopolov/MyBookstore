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
    public sealed class JournalMemoryRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _photoImage;
        [SerializeField] private Sprite _photoFallback;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private GameObject _goldenMarker;

        private CancellationTokenSource _photoCts;

        public void Bind(JournalMemoryItemModel model, IUiSpriteProvider sprites)
        {
            if (_titleLabel != null) _titleLabel.text = model.TitleKey;
            if (_descriptionLabel != null) _descriptionLabel.text = model.DescriptionKey;
            if (_goldenMarker != null) _goldenMarker.SetActive(model.IsGolden);

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
            if (_goldenMarker != null) _goldenMarker.SetActive(false);
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
