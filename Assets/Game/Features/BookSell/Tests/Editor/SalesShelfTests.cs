using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesShelfTests
    {
        [Test]
        public void AvailableForSelection_ReturnsAllAvailable_Initially()
        {
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1"), SalesTestKit.Book("b2"));
            var available = shelf.AvailableForSelection().Select(x => x.BookId).ToArray();
            CollectionAssert.AreEquivalent(new[] { "b1", "b2" }, available);
        }

        [Test]
        public void Reserve_HidesFromAvailable_AndSetsReservedFlag()
        {
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1"), SalesTestKit.Book("b2"));

            Assert.IsTrue(shelf.Reserve("b1"));
            Assert.IsTrue(shelf.IsReserved("b1"));
            CollectionAssert.AreEquivalent(new[] { "b2" }, shelf.AvailableForSelection().Select(x => x.BookId));
        }

        [Test]
        public void Reserve_Twice_SecondFails()
        {
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1"));
            Assert.IsTrue(shelf.Reserve("b1"));
            Assert.IsFalse(shelf.Reserve("b1"));
        }

        [Test]
        public void ReleaseReserve_RestoresAvailability()
        {
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1"));
            shelf.Reserve("b1");
            shelf.ReleaseReserve("b1");

            Assert.IsFalse(shelf.IsReserved("b1"));
            CollectionAssert.AreEquivalent(new[] { "b1" }, shelf.AvailableForSelection().Select(x => x.BookId));
        }

        [Test]
        public void CommitSale_MarksSoldOut_AndClearsReservation()
        {
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1"));
            shelf.Reserve("b1");
            shelf.CommitSale("b1");

            Assert.AreEqual(ShelfBookState.SoldOut, shelf.Find("b1").State);
            Assert.IsFalse(shelf.IsReserved("b1"));
            Assert.IsEmpty(shelf.AvailableForSelection());
        }

        [Test]
        public void AllSoldOut_TrueOnlyWhenEveryBookSold()
        {
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1"), SalesTestKit.Book("b2"));
            Assert.IsFalse(shelf.AllSoldOut());

            shelf.CommitSale("b1");
            Assert.IsFalse(shelf.AllSoldOut());

            shelf.CommitSale("b2");
            Assert.IsTrue(shelf.AllSoldOut());
        }

        [Test]
        public void AllSoldOut_EmptyShelf_IsFalse()
        {
            var shelf = new SalesShelf();
            Assert.IsFalse(shelf.AllSoldOut());
        }
    }
}
