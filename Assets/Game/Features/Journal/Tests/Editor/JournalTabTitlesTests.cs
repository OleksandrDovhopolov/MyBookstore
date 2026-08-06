using Game.Journal.UI;
using NUnit.Framework;

namespace Game.Journal.UI.Tests.Editor
{
    public sealed class JournalTabTitlesTests
    {
        [TestCase(JournalTab.Memories, "Memories")]
        [TestCase(JournalTab.Places, "Locations")]
        [TestCase(JournalTab.Objects, "Decorations")]
        [TestCase(JournalTab.People, "Characters")]
        [TestCase(JournalTab.Quests, "Quests")]
        public void Get_ReturnsTitle_ForTab(JournalTab tab, string expected)
        {
            Assert.AreEqual(expected, JournalTabTitles.Get(tab));
        }
    }
}
