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
    public sealed class JournalPlaceRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _locationImage;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private GameObject _lockedPanel;
        [SerializeField] private UIListPool<JournalGenreIconView> _genrePool = new();

        private string _locationId;
        private CancellationTokenSource _iconCts;

        public void Bind(JournalPlaceItemModel model, IUiSpriteProvider sprites)
        {
            _locationId = model.LocationId;
            if (_nameLabel != null) _nameLabel.text = model.DisplayName;
            if (_lockedPanel != null) _lockedPanel.SetActive(!model.IsUnlocked);
            RenderGenres(model.DemandGenres);

            CancelIconLoad();
            if (_locationImage != null) _locationImage.sprite = null;
            if (sprites == null) return;

            _iconCts = new CancellationTokenSource();
            LoadIconsAsync(model, sprites, _iconCts.Token).Forget();
        }

        private void RenderGenres(System.Collections.Generic.IReadOnlyList<string> genres)
        {
            _genrePool.DisableAll();

            if (genres != null)
            {
                for (var i = 0; i < genres.Count; i++)
                {
                    if (string.IsNullOrEmpty(genres[i])) continue;
                    _genrePool.GetNext().Bind(genres[i], null);
                }
            }

            _genrePool.DisableNonActive();
        }

        private async UniTaskVoid LoadIconsAsync(JournalPlaceItemModel model, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                if (_locationImage != null && !string.IsNullOrEmpty(model.LocationId))
                {
                    var locationSprite = await sprites.GetSpriteAsync(model.LocationId, ct);
                    if (ct.IsCancellationRequested) return;
                    if (_locationImage != null) _locationImage.sprite = locationSprite;
                }

                var items = _genrePool.ActiveElements();
                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrEmpty(item.Genre)) continue;
                    var sprite = await sprites.GetSpriteAsync(item.Genre, ct);
                    if (ct.IsCancellationRequested) return;
                    item.SetIcon(sprite);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Cleanup()
        {
            _locationId = null;
            CancelIconLoad();
            if (_locationImage != null) _locationImage.sprite = null;
            if (_nameLabel != null) _nameLabel.text = string.Empty;
            if (_lockedPanel != null) _lockedPanel.SetActive(false);
            _genrePool.DisableAll();
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
