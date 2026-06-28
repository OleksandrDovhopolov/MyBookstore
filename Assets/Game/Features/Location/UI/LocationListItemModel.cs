using System.Collections.Generic;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.LocationEntry.API;
using Game.LocationUnlock.API;

namespace Game.Location.UI
{
    public sealed class LocationListItemModel
    {
        public string LocationId { get; }
        public string DisplayName { get; }
        public bool IsUnlocked { get; }
        public int EntryCost { get; }
        public string EntryCurrencyId { get; }
        public bool CanAffordEntry { get; }
        public bool StartEnabled { get; }
        public IReadOnlyList<LocationConditionProgress> Conditions { get; }

        public LocationListItemModel(string locationId, string displayName, bool isUnlocked,
            int entryCost, string entryCurrencyId, bool canAffordEntry,
            IReadOnlyList<LocationConditionProgress> conditions)
        {
            LocationId = locationId;
            DisplayName = displayName;
            IsUnlocked = isUnlocked;
            EntryCost = entryCost;
            EntryCurrencyId = entryCurrencyId;
            CanAffordEntry = canAffordEntry;
            StartEnabled = isUnlocked && canAffordEntry;
            Conditions = conditions ?? System.Array.Empty<LocationConditionProgress>();
        }

        public static LocationListItemModel From(
            LocationConfig config, LocationUnlockStatus status, LocationEntryCost entryCost, bool canAffordEntry)
        {
            var unlocked = status != null && status.State == LocationUnlockState.Unlocked;

            List<LocationConditionProgress> conditions = null;
            if (!unlocked && status != null)
            {
                conditions = new List<LocationConditionProgress>();
                CollectLeaves(status.Progress, conditions);
            }

            return new LocationListItemModel(
                config.Id, config.DisplayName, unlocked,
                entryCost.Total, entryCost.CurrencyId, canAffordEntry, conditions);
        }

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

        private static string ExtractGenre(string reasonKey)
        {
            if (string.IsNullOrEmpty(reasonKey)) return reasonKey;
            var dot = reasonKey.LastIndexOf('.');
            return dot >= 0 && dot < reasonKey.Length - 1 ? reasonKey.Substring(dot + 1) : reasonKey;
        }
    }
}
