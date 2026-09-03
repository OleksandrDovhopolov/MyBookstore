using Game.Journal.UI;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    public sealed class JournalTabTitlesTests
    {
        [TestCase(JournalTab.Memories, "ui.journal.tab.memories")]
        [TestCase(JournalTab.Places, "ui.journal.tab.locations")]
        [TestCase(JournalTab.Objects, "ui.journal.tab.objects")]
        [TestCase(JournalTab.People, "ui.journal.tab.characters")]
        [TestCase(JournalTab.Quests, "ui.journal.tab.quests")]
        public void Get_ReturnsTitle_ForTab(JournalTab tab, string expected)
        {
            Assert.AreEqual(expected, JournalTabTitles.Get(tab));
        }
    }
}
