using System.Collections.Generic;

namespace Game.Journal.UI
{
    public sealed class JournalPlaceItemModel
    {
        public JournalPlaceItemModel(
            string locationId,
            string displayName,
            bool isUnlocked,
            IReadOnlyList<string> demandGenres)
        {
            LocationId = locationId;
            DisplayName = displayName;
            IsUnlocked = isUnlocked;
            DemandGenres = demandGenres ?? System.Array.Empty<string>();
        }

        public string LocationId { get; }
        public string DisplayName { get; }
        public bool IsUnlocked { get; }
        public IReadOnlyList<string> DemandGenres { get; }
    }
}
