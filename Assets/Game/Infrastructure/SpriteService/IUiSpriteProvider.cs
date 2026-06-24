using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Newspaper.UI
{
    /// <summary>
    /// Long-lived (singleton) async sprite cache for newspaper/rewards UI. Loads sprites from
    /// Addressables by id on first request and keeps them cached for the whole session, so repeated
    /// window opens resolve instantly (cache hit) instead of reloading on every open.
    /// </summary>
    public interface IUiSpriteProvider
    {
        /// <summary>
        /// Warms the cache from <see cref="UiSpriteCatalog"/>. Currently a no-op (the catalog is empty);
        /// kept so future catalog entries can be eagerly preloaded at bootstrap.
        /// </summary>
        UniTask PreloadAsync(CancellationToken ct);

        /// <summary>
        /// Returns the sprite for <paramref name="id"/> (its Addressables address), loading and caching
        /// it on first request. Returns <c>null</c> for an empty id or a failed load.
        /// </summary>
        UniTask<Sprite> GetSpriteAsync(string id, CancellationToken ct);

        /// <summary>Releases all cached sprites. Called at a controlled app point, never from windows.</summary>
        void ReleaseAll();
    }
}
