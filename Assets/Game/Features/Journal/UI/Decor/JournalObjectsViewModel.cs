using System.Collections.Generic;

namespace Game.Journal.UI
{
    public sealed class JournalObjectsViewModel
    {
        public JournalObjectsViewModel(
            IReadOnlyList<JournalObjectItemModel> objects,
            IReadOnlyList<JournalBonusItemModel> bonuses)
        {
            Objects = objects ?? System.Array.Empty<JournalObjectItemModel>();
            Bonuses = bonuses ?? System.Array.Empty<JournalBonusItemModel>();
        }

        public IReadOnlyList<JournalObjectItemModel> Objects { get; }
        public IReadOnlyList<JournalBonusItemModel> Bonuses { get; }
    }
}
