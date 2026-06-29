using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Newspaper.UI;
using TMPro;
using UIShared;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Characters.UI
{
    /// <summary>
    /// One character row. Locked (undiscovered) characters show <c>_lockedPanel</c> as a placeholder and
    /// load no portrait; discovered characters load their portrait by <c>PortraitKey</c> via the shared
    /// async sprite cache. Mirrors <see cref="Game.Location.UI.LocationRowView"/>.
    /// </summary>
    public sealed class JournalCharacterRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _portraitImage;
        [SerializeField] private GameObject _lockedPanel;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _memoryCountLabel;

        private CancellationTokenSource _portraitCts;

        public void Bind(JournalCharacterItemModel model, IUiSpriteProvider sprites)
        {
            if (_nameLabel != null) _nameLabel.text = model.DisplayNameKey;
            if (_memoryCountLabel != null)
                _memoryCountLabel.text = $"{model.UnlockedMemoryCount}/{model.TotalMemoryCount}";
            if (_lockedPanel != null) _lockedPanel.SetActive(model.Locked);

            CancelPortraitLoad();
            if (_portraitImage != null) _portraitImage.enabled = false;

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
                _portraitImage.sprite = sprite;
                _portraitImage.enabled = sprite != null;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Cleanup()
        {
            CancelPortraitLoad();
            if (_portraitImage != null) _portraitImage.sprite = null;
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
