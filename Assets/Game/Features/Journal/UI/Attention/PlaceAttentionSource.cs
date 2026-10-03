using System;
using System.Collections.Generic;
using Game.Attention.API;
using Game.Configs;
using Game.Configs.Models;
using Game.LocationUnlock.API;

namespace Game.Journal.UI
{
    /// <summary>Ids of every currently unlocked location — the Journal Places tab badge.</summary>
    public sealed class PlaceAttentionSource : IAttentionSource, IDisposable
    {
        private readonly ILocationUnlockService _locations;
        private readonly IConfigsService _configs;

        public PlaceAttentionSource(ILocationUnlockService locations, IConfigsService configs)
        {
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));

            _locations.Unlocked += OnLocationChanged;
            _locations.StatusChanged += OnLocationChanged;
        }

        public string Key => JournalAttentionKeys.Places;

        public IEnumerable<string> CurrentIds
        {
            get
            {
                var locations = _configs.GetAll<LocationConfig>();
                if (locations == null) yield break;

                for (var i = 0; i < locations.Count; i++)
                {
                    var location = locations[i];
                    if (location == null || string.IsNullOrEmpty(location.Id)) continue;
                    if (_locations.IsUnlocked(location.Id))
                        yield return location.Id;
                }
            }
        }

        public event Action Changed;

        public void Dispose()
        {
            _locations.Unlocked -= OnLocationChanged;
            _locations.StatusChanged -= OnLocationChanged;
        }

        private void OnLocationChanged(string _) => Changed?.Invoke();
    }
}
