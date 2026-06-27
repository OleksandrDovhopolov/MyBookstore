using System.Collections.Generic;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.LocationUnlock.API;

namespace Game.Location.UI
{
    /// <summary>
    /// Presentation model for one row in the Location Window. Built by the controller from a
    /// <see cref="LocationConfig"/> + the live <see cref="LocationUnlockStatus"/>, so the view never
    /// touches raw configs or the condition <see cref="JObject"/>: unlocked rows show only Start,
    /// locked rows show <see cref="Conditions"/> (genre icon + "current/target").
    /// </summary>
    public sealed class LocationListItemModel
    {
        public string LocationId { get; }
        public string DisplayName { get; }
        public bool IsUnlocked { get; }
        public bool StartEnabled { get; }
        public IReadOnlyList<LocationConditionProgress> Conditions { get; }

        public LocationListItemModel(string locationId, string displayName, bool isUnlocked,
            bool startEnabled, IReadOnlyList<LocationConditionProgress> conditions)
        {
            LocationId = locationId;
            DisplayName = displayName;
            IsUnlocked = isUnlocked;
            StartEnabled = startEnabled;
            Conditions = conditions ?? System.Array.Empty<LocationConditionProgress>();
        }

        public static LocationListItemModel From(LocationConfig config, LocationUnlockStatus status)
        {
            var unlocked = status != null && status.State == LocationUnlockState.Unlocked;

            List<LocationConditionProgress> conditions = null;
            if (!unlocked && status != null)
            {
                conditions = new List<LocationConditionProgress>();
                CollectLeaves(status.Progress, conditions);
            }

            return new LocationListItemModel(
                config.Id, config.DisplayName, unlocked, startEnabled: unlocked, conditions);
        }

        // Only leaves carry a concrete requirement; composite all/any/not nodes are flattened away.
        private static void CollectLeaves(ConditionResult node, List<LocationConditionProgress> result)
        {
            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                    CollectLeaves(child, result);
                return;
            }

            result.Add(new LocationConditionProgress(
                ExtractGenre(node.ReasonKey), node.Current, node.Target, node.IsMet));
        }

        // "soldGenre.Crime" → "Crime" (sprite/Addressables id); falls back to the raw key.
        private static string ExtractGenre(string reasonKey)
        {
            if (string.IsNullOrEmpty(reasonKey)) return reasonKey;
            var dot = reasonKey.LastIndexOf('.');
            return dot >= 0 && dot < reasonKey.Length - 1 ? reasonKey.Substring(dot + 1) : reasonKey;
        }
    }
}
