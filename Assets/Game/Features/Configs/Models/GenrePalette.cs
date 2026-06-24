using System;
using UnityEngine;

namespace Game.Configs.Models
{
    /// <summary>
    /// Designer-editable mapping of <see cref="BookGenre"/> to a UI color. Used by the Preparation
    /// shelf preview to tint proportional genre segments. Create via
    /// <c>Create → Game/UI/GenrePalette</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "GenrePalette", menuName = "Game/UI/GenrePalette")]
    public sealed class GenrePalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public BookGenre Genre;
            public Color Color;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField] private Color _fallback = Color.gray;

        /// <summary>Returns the configured color for <paramref name="genre"/>, or the fallback color.</summary>
        public Color GetColor(BookGenre genre)
        {
            for (var i = 0; i < _entries.Length; i++)
                if (_entries[i].Genre == genre)
                    return _entries[i].Color;

            return _fallback;
        }
    }
}
