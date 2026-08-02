using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SpriteService;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Characters.UI
{
    /// <summary>
    /// One character row. Locked (undiscovered) characters show <c>_lockedPanel</c> as a placeholder and
    /// load no portrait; discovered characters load their portrait by <c>PortraitKey</c> via the shared
    /// async sprite cache. Mirrors <see cref="FilePathAttribute.Location.UI.LocationRowView"/>.
    /// </summary>
    public sealed class JournalCharacterRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Sprite _spriteFallback;
        [SerializeField] private GameObject _lockedPanel;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _discoveryStatusLabel;
        [SerializeField] private TextMeshProUGUI _memoryCountLabel;

        private CancellationTokenSource _portraitCts;

        public void Bind(JournalCharacterItemModel model, IUiSpriteProvider sprites)
        {
            if (_nameLabel != null) _nameLabel.text = model.DisplayNameKey;
            if (_discoveryStatusLabel != null)
                _discoveryStatusLabel.text = model.IsDiscovered
                    ? "Персонаж разблокирован"
                    : "Персонаж не разблокирован";
            if (_memoryCountLabel != null)
                _memoryCountLabel.text = $"{model.UnlockedMemoryCount}/{model.TotalMemoryCount}";
            if (_lockedPanel != null) _lockedPanel.SetActive(model.Locked);

            CancelPortraitLoad();
            SetPortrait(_spriteFallback);

            // Locked → placeholder only, no portrait load.
            if (model.Locked || string.IsNullOrEmpty(model.PortraitKey) || sprites == null) return;

            _portraitCts = new CancellationTokenSource();
            LoadPortraitAsync(model.PortraitKey, sprites, _portraitCts.Token).Forget();
        }

        private async UniTaskVoid LoadPortraitAsync(string portraitKey, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(portraitKey, ct);
                if (ct.IsCancellationRequested || _portraitImage == null) return;
                SetPortrait(sprite != null ? sprite : _spriteFallback);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Journal] failed to load portrait '{portraitKey}': {e.Message}");
                SetPortrait(_spriteFallback);
            }
        }

        public void Cleanup()
        {
            CancelPortraitLoad();
            SetPortrait(null);
        }

        private void SetPortrait(Sprite sprite)
        {
            if (_portraitImage == null) return;
            _portraitImage.sprite = sprite;
            _portraitImage.enabled = sprite != null;
        }

        private void CancelPortraitLoad()
        {
            if (_portraitCts == null) return;
            _portraitCts.Cancel();
            _portraitCts.Dispose();
            _portraitCts = null;
        }

        private void OnDestroy() => CancelPortraitLoad();
    }
}
