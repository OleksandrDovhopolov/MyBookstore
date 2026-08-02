using System;
using System.Collections.Generic;
using Game.Configs.Models;
using Game.UI.ContentWidget;
using UnityEngine;

namespace Game.Location.UI
{
    public sealed class LocationDemandWidgetData : ContentWidgetDataBase
    {
        public string LocationDisplayName { get; }
        public IReadOnlyList<LocationDemandGenre> Genres { get; }

        public LocationDemandWidgetData(string locationDisplayName, IReadOnlyList<LocationDemandGenre> genres)
        {
            LocationDisplayName = locationDisplayName ?? string.Empty;
            Genres = genres ?? Array.Empty<LocationDemandGenre>();
        }
    }

    public readonly struct LocationDemandGenre
    {
        public BookGenre Genre { get; }
        public Sprite Icon { get; }

        public LocationDemandGenre(BookGenre genre, Sprite icon)
        {
            Genre = genre;
            Icon = icon;
        }
    }
}
