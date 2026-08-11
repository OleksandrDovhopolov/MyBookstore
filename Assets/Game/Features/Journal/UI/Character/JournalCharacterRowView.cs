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
    /// <summary>
    /// One character row. Locked (undiscovered) characters show <c>_lockedPanel</c> as a placeholder and
    /// load no portrait; discovered characters load their portrait by <c>PortraitKey</c> via the shared
    /// async sprite cache. Mirrors <see cref="FilePathAttribute.Location.UI.LocationRowView"/>.
    /// </summary>
    public sealed class JournalCharacterRowView : MonoBehaviour, ICleanup
    {
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Sprite _spriteFallback;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private UIListPool<JournalGenreIconView> _genrePool = new();

        private CancellationTokenSource _portraitCts;
        private CancellationTokenSource _genreCts;

        public void Bind(JournalCharacterItemModel model, IUiSpriteProvider sprites)
        {
            if (_nameLabel != null) _nameLabel.text = model.DisplayNameKey;
            RenderGenres(model.Locked ? null : model.FavoriteGenres, sprites);

            CancelPortraitLoad();
            SetPortrait(_spriteFallback);

            // Locked rows use the placeholder only, with no portrait load.
            if (model.Locked || string.IsNullOrEmpty(model.PortraitKey) || sprites == null) return;

            _portraitCts = new CancellationTokenSource();
            LoadPortraitAsync(model.PortraitKey, sprites, _portraitCts.Token).Forget();
        }

        private async UniTaskVoid LoadPortraitAsync(string portraitKey, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var avatarSprite = portraitKey + "_avatar";
                var sprite = await sprites.GetSpriteAsync(avatarSprite, ct);
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

        private void RenderGenres(System.Collections.Generic.IReadOnlyList<string> genres, IUiSpriteProvider sprites)
        {
            CancelGenreLoad();
            _genrePool.DisableAll();

            if (genres != null)
            {
                for (var i = 0; i < genres.Count; i++)
                {
                    var genre = genres[i];
                    if (string.IsNullOrEmpty(genre)) continue;
                    _genrePool.GetNext().Bind(genre, null);
                }
            }

            _genrePool.DisableNonActive();
            if (sprites == null) return;

            _genreCts = new CancellationTokenSource();
            LoadGenreIconsAsync(sprites, _genreCts.Token).Forget();
        }

        private async UniTaskVoid LoadGenreIconsAsync(IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
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
            CancelPortraitLoad();
            CancelGenreLoad();
            SetPortrait(null);
            _genrePool.DisableAll();
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

        private void CancelGenreLoad()
        {
            if (_genreCts == null) return;
            _genreCts.Cancel();
            _genreCts.Dispose();
            _genreCts = null;
        }

        private void OnDestroy()
        {
            CancelPortraitLoad();
            CancelGenreLoad();
        }
    }
}
