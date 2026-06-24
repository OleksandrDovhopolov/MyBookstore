using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Infrastructure;
using UnityEngine;

namespace Game.Newspaper.UI
{
    /// <summary>
    /// Default <see cref="IUiSpriteProvider"/>. Loads sprites from Addressables by id through
    /// <see cref="ProdAddressablesWrapper"/> (ref-counted cache) and keeps the references in a single
    /// dictionary for the whole session. A failed load logs a warning and yields <c>null</c>.
    /// </summary>
    public sealed class UiSpriteProvider : IUiSpriteProvider
    {
        private const string LogPrefix = "[UiSpriteProvider]";

        private readonly UiSpriteCatalog _catalog;
        private readonly Dictionary<string, Sprite> _cache = new(StringComparer.Ordinal);

        public UiSpriteProvider(UiSpriteCatalog catalog)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
        }

        public async UniTask PreloadAsync(CancellationToken ct)
        {
            // Currently the catalog is empty, so this is a no-op. Once entries are authored, this
            // warms the cache at bootstrap (each GetSpriteAsync is a no-op afterwards).
            var entries = _catalog.SpritesById;
            if (entries == null) return;

            for (var i = 0; i < entries.Length; i++)
            {
                var address = entries[i].Address;
                if (string.IsNullOrWhiteSpace(address)) continue;
                await GetSpriteAsync(address, ct);
            }
        }

        public async UniTask<Sprite> GetSpriteAsync(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (_cache.TryGetValue(id, out var cached))
                return cached;

            Sprite sprite;
            try
            {
                sprite = await ProdAddressablesWrapper.LoadAsync<Sprite>(id, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Failed to load sprite '{id}': {ex.Message}");
                return null;
            }

            // Another concurrent request may have cached the same id while we awaited: drop our extra
            // ref so the ref-counted cache stays balanced for ReleaseAll, and reuse the cached sprite.
            if (_cache.TryGetValue(id, out cached))
            {
                if (sprite != null)
                    ProdAddressablesWrapper.Release(sprite);
                return cached;
            }

            if (sprite != null)
                _cache[id] = sprite;

            return sprite;
        }

        public void ReleaseAll()
        {
            ProdAddressablesWrapper.ReleaseGroup(_cache.Keys);
            _cache.Clear();
        }
    }
}
