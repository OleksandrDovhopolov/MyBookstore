using System.Collections.Generic;

namespace Game.Journal.UI
{
    public sealed class SavedJournalAttention
    {
        public HashSet<string> SeenQuestNewIds { get; set; } = new();
        public HashSet<string> SeenQuestAwardIds { get; set; } = new();
        public HashSet<string> SeenPlaceIds { get; set; } = new();
        public HashSet<string> SeenPeopleIds { get; set; } = new();
    }
}
