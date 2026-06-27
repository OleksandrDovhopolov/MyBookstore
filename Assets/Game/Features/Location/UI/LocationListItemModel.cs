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
    /// locked rows show <see cref="ProgressLines"/> ("Crime 3/10").
    /// </summary>
    public sealed class LocationListItemModel
    {
        public string LocationId { get; }
        public string DisplayName { get; }
        public bool IsUnlocked { get; }
        public bool StartEnabled { get; }
        public IReadOnlyList<string> ProgressLines { get; }

        public LocationListItemModel(string locationId, string displayName, bool isUnlocked,
            bool startEnabled, IReadOnlyList<string> progressLines)
        {
            LocationId = locationId;
            DisplayName = displayName;
            IsUnlocked = isUnlocked;
            StartEnabled = startEnabled;
            ProgressLines = progressLines ?? System.Array.Empty<string>();
        }

        public static LocationListItemModel From(LocationConfig config, LocationUnlockStatus status)
        {
            var unlocked = status != null && status.State == LocationUnlockState.Unlocked;

            List<string> lines = null;
            if (!unlocked && status != null)
            {
                lines = new List<string>();
                CollectLeafLines(status.Progress, lines);
            }

            return new LocationListItemModel(
                config.Id, config.DisplayName, unlocked, startEnabled: unlocked, lines);
        }

        // Only leaves carry a human requirement ("Crime 3/10"); composite all/any/not nodes are flattened.
        private static void CollectLeafLines(ConditionResult node, List<string> lines)
        {
            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                    CollectLeafLines(child, lines);
                return;
            }

            lines.Add(FormatLeaf(node));
        }

        private static string FormatLeaf(ConditionResult node)
        {
            var label = node.ReasonKey ?? "requirement";
            var dot = label.LastIndexOf('.');           // "soldGenre.Crime" → "Crime"
            if (dot >= 0 && dot < label.Length - 1)
                label = label.Substring(dot + 1);

            var mark = node.IsMet ? "✓" : "•";
            return node.Target > 0
                ? $"{mark} {label} {node.Current}/{node.Target}"
                : $"{mark} {label}";
        }
    }
}
