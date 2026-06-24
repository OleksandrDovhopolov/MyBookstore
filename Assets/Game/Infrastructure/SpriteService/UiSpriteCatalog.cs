using System;
using UnityEngine;

namespace Game.Newspaper.UI
{
    /// <summary>
    /// Authoring asset for <see cref="IUiSpriteProvider"/>. Maps a sprite id to its Addressables
    /// address so the provider can optionally preload them at bootstrap. Currently left empty —
    /// sprites are loaded on demand by id; this is a placeholder for future eager preloading.
    /// Create via <c>Create → Game/UI/UiSpriteCatalog</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSpriteCatalog", menuName = "Game/UI/UiSpriteCatalog")]
    public sealed class UiSpriteCatalog : ScriptableObject
    {
        [Serializable]
        public struct KeyedSpriteAddress
        {
            [Tooltip("Logical sprite id (lot id / resource id / genre).")]
            public string Key;

            [Tooltip("Addressables address of the sprite.")]
            public string Address;
        }

        [Tooltip("Sprites to preload, keyed by id. Empty for now.")]
        [SerializeField] private KeyedSpriteAddress[] _spritesById = Array.Empty<KeyedSpriteAddress>();

        public KeyedSpriteAddress[] SpritesById => _spritesById;

    }
}
