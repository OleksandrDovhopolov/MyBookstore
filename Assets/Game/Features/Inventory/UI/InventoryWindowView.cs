using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Inventory.API;
using Game.UI;
using SpriteService;
using UIShared;
using UnityEngine;

namespace Game.Inventory.UI
{
    public class InventoryWindowView : WindowView
    {
        [Header("List")]
        [SerializeField] private UIListPool<InventoryItemRowView> _rowPool = new();

        private IInventoryService _inventory;
        private IUiSpriteProvider _sprites;
        private IConfigsService _configs;
        private IDecorPlacementService _decorPlacement;
        private Action<string> _onDecorInfo;

        private readonly CancellationTokenSource _cts = new();
        private CancellationTokenSource _renderCts;
        private bool _isBound;

        /// <summary>
        /// Called once by the controller in OnInit. Stores deps and subscribes to inventory
        /// changes. Subsequent re-shows go through <see cref="Refresh"/>.
        /// </summary>
        public void Bind(
            IInventoryService inventory,
            IUiSpriteProvider sprites,
            IConfigsService configs,
            IDecorPlacementService decorPlacement,
            Action<string> onDecorInfo)
        {
            if (_isBound) return;

            _inventory = inventory;
            _sprites = sprites;
            _configs = configs;
            _decorPlacement = decorPlacement;
            _onDecorInfo = onDecorInfo;

            if (_inventory == null || _configs == null)
            {
                Debug.LogWarning("[InventoryWindowView] dependencies missing — not registered in DI?");
                return;
            }

            _inventory.Changed += OnInventoryChanged;
            if (_decorPlacement != null) _decorPlacement.PlacementChanged += OnDecorPlacementChanged;
            _isBound = true;
            Render();
        }

        public void Refresh()
        {
            if (!_isBound) return;
            Render();
        }

        public void Teardown()
        {
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            if (_decorPlacement != null) _decorPlacement.PlacementChanged -= OnDecorPlacementChanged;
            CancelRender();
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _cts.Dispose();
            _isBound = false;
        }

        private void Render()
        {
            ClearRows();
            if (_rowPool == null) return;

            _renderCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            var countsByGenre = BuildBookGenreCounts();
            var genres = Enum.GetValues(typeof(BookGenre)).Cast<BookGenre>().ToList();

            for (var i = 0; i < genres.Count; i++)
            {
                var genre = genres[i];
                countsByGenre.TryGetValue(genre, out var count);
                var row = _rowPool.GetNext();
                row.BindGenre(genre, count, _sprites, _renderCts.Token);
            }

            var decorItems = _inventory.GetByCategory(InventoryCategories.Decor)
                .OrderBy(it => it.ItemId, StringComparer.Ordinal)
                .ToList();
            for (var i = 0; i < decorItems.Count; i++)
            {
                var item = decorItems[i];
                if (!_configs.TryGet<DecorConfig>(item.ItemId, out var decor) || decor == null) continue;

                var row = _rowPool.GetNext();
                row.BindDecor(decor, IsDecorPlaced(item.ItemId), _sprites, _onDecorInfo, _renderCts.Token);
            }

            _rowPool.DisableNonActive();
        }

        private bool IsDecorPlaced(string decorId)
        {
            if (_decorPlacement == null || string.IsNullOrEmpty(decorId)) return false;

            foreach (var entry in _decorPlacement.GetAllPlacements())
            {
                if (entry != null && string.Equals(entry.DecorId, decorId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private Dictionary<BookGenre, int> BuildBookGenreCounts()
        {
            var counts = new Dictionary<BookGenre, int>();
            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
                counts[genre] = 0;

            var items = _inventory.GetByCategory(InventoryCategories.Book);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!_configs.TryGet<BookConfig>(item.ItemId, out var book) || book == null) continue;
                if (!BookGenreExtensions.TryParseGenre(book.PrimaryGenre, out var genre)) continue;

                counts[genre] += item.Count;
            }

            return counts;
        }

        private void ClearRows()
        {
            CancelRender();
            _rowPool?.DisableAll();
        }

        private void CancelRender()
        {
            if (_renderCts == null) return;
            _renderCts.Cancel();
            _renderCts.Dispose();
            _renderCts = null;
        }

        private void OnInventoryChanged(InventoryChangeEvent _)
        {
            if (_isBound) Render();
        }

        private void OnDecorPlacementChanged()
        {
            if (_isBound) Render();
        }
    }
}
