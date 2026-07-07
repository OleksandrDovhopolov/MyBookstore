using System.Collections.Generic;
using Game.UI.ContentWidget;
using UnityEngine;

namespace Game.Decor.UI
{
    public sealed class DecorInfoWidgetData : ContentWidgetDataBase
    {
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public IReadOnlyList<BonusRow> Bonuses { get; }
        public IReadOnlyList<string> Characteristics { get; }

        public DecorInfoWidgetData(
            string name,
            string description,
            Sprite icon,
            IReadOnlyList<BonusRow> bonuses,
            IReadOnlyList<string> characteristics)
        {
            Name = name;
            Description = description;
            Icon = icon;
            Bonuses = bonuses ?? new List<BonusRow>();
            Characteristics = characteristics ?? new List<string>();
        }

        public sealed class BonusRow
        {
            public string Genre { get; }
            public string PercentText { get; }
            public Color PercentColor { get; }
            public Sprite GenreSprite { get; }

            public BonusRow(string genre, string percentText, Color percentColor, Sprite genreSprite)
            {
                Genre = genre;
                PercentText = percentText;
                PercentColor = percentColor;
                GenreSprite = genreSprite;
            }
        }
    }
}
