using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Game.Configs;
using Game.Configs.Models;
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
            IConfigsService configs)
        {
            if (_isBound) return;

            _inventory = inventory;
            _sprites = sprites;
            _configs = configs;

            if (_inventory == null || _configs == null)
            {
                Debug.LogWarning("[InventoryWindowView] dependencies missing — not registered in DI?");
                return;
            }

            _inventory.Changed += OnInventoryChanged;
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
                row.Bind(genre, count, _sprites, _renderCts.Token);
            }

            _rowPool.DisableNonActive();
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
    }
}
