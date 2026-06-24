using System;
using System.Collections.Generic;
using Game.Configs.Models;
using Game.Preparation.Domain;
using UIShared;
using UnityEngine;

namespace Game.Preparation.UI
{
    /// <summary>
    /// Horizontal bar that visualises the current shelf selection as proportional, color-coded genre
    /// segments (only genres with a positive picked count are shown). Renders from the shared
    /// preparation model — it holds no selection state of its own. Tapping a segment forwards the
    /// genre id so the controller can remove one book.
    /// </summary>
    public sealed class ShelfPreviewView : MonoBehaviour
    {
        [SerializeField] private GenrePalette _palette;
        [SerializeField] private UIListPool<ShelfPreviewSegmentView> _segmentPool = new();
        [Tooltip("Optional placeholder shown when nothing is picked (e.g. \"No books selected\").")]
        [SerializeField] private GameObject _emptyState;

        public void Render(IReadOnlyList<GenreSelectionItem> items, Action<string> onSegmentClicked)
        {
            _segmentPool.DisableAll();

            var anyPicked = false;
            if (items != null)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null || item.Quantity <= 0) continue;

                    anyPicked = true;
                    _segmentPool.GetNext().Bind(item.Genre, ResolveColor(item.Genre), item.Quantity, onSegmentClicked);
                }
            }

            _segmentPool.DisableNonActive();

            if (_emptyState != null) _emptyState.SetActive(!anyPicked);
        }

        private Color ResolveColor(string genreId)
        {
            if (_palette != null && BookGenreExtensions.TryParseGenre(genreId, out var genre))
                return _palette.GetColor(genre);

            return Color.gray;
        }
    }
}
