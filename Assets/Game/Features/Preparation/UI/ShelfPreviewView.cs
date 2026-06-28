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

        // Reused between renders so sorting the picked genres allocates nothing per update.
        private readonly List<GenreSelectionItem> _ordered = new();

        // Largest picked count first (left → right). Ties broken by genre id for a stable order.
        private static readonly Comparison<GenreSelectionItem> ByPickedDescending = (a, b) =>
        {
            var byQuantity = b.Quantity.CompareTo(a.Quantity);
            return byQuantity != 0 ? byQuantity : string.CompareOrdinal(a.Genre, b.Genre);
        };

        public void Render(IReadOnlyList<GenreSelectionItem> items, Action<string> onSegmentClicked)
        {
            _segmentPool.DisableAll();

            _ordered.Clear();
            if (items != null)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item != null && item.Quantity > 0)
                        _ordered.Add(item);
                }
            }

            _ordered.Sort(ByPickedDescending);

            for (var i = 0; i < _ordered.Count; i++)
            {
                var item = _ordered[i];
                _segmentPool.GetNext().Bind(item.Genre, ResolveColor(item.Genre), item.Quantity, onSegmentClicked);
            }

            _segmentPool.DisableNonActive();

            if (_emptyState != null) _emptyState.SetActive(_ordered.Count == 0);
        }

        private Color ResolveColor(string genreId)
        {
            if (_palette != null && BookGenreExtensions.TryParseGenre(genreId, out var genre))
                return _palette.GetColor(genre);

            return Color.gray;
        }
    }
}
