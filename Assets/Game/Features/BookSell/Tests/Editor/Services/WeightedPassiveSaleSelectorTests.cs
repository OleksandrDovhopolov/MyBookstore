using System;
using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class WeightedPassiveSaleSelectorTests
    {
        private static BookConfig Book(string id, string genre, float rarity = 0.5f)
        {
            var b = SalesTestKit.Book(id, genre: genre);
            b.RarityWeight = rarity;
            return b;
        }

        private static WeightedPassiveSaleSelector Sel(double chance) =>
            new(new FakeBaseSaleChanceCalculator(chance));

        [Test]
        public void StageOneHit_SingleBook_ReturnsThatBook_GenrePopulated_TagsEmpty()
        {
            var shelfBook = new ShelfBook(Book("b1", "Fantasy"));
            var random = new FakeSalesRandom().EnqueueDouble(0.0); // stage-1 passes

            var result = Sel(1.0).PickPassiveSale(
                new[] { shelfBook }, SalesTestKit.Location(), Array.Empty<string>(), random);

            Assert.IsNotNull(result);
            Assert.AreEqual("b1", result.Book.BookId);
            CollectionAssert.AreEqual(new[] { "Fantasy" }, result.MatchedGenres.ToArray());
            Assert.IsEmpty(result.MatchedTags);
        }

        [Test]
        public void ZeroChance_ReturnsNull()
        {
            var shelfBook = new ShelfBook(Book("b1", "Fantasy"));

            var result = Sel(0.0).PickPassiveSale(
                new[] { shelfBook }, SalesTestKit.Location(), Array.Empty<string>(), new FakeSalesRandom());

            Assert.IsNull(result);
        }

        [Test]
        public void MultipleWinnerGenres_PicksUniformlyByRange()
        {
            var fantasy = new ShelfBook(Book("bF", "Fantasy"));
            var crime = new ShelfBook(Book("bC", "Crime"));
            // Both genres pass stage-1 (chance=1.0 vs 0.0 in NextDouble), Range picks index 1.
            var random = new FakeSalesRandom()
                .EnqueueDouble(0.0, 0.0)
                .EnqueueRangeIndex(1);

            var result = Sel(1.0).PickPassiveSale(
                new[] { fantasy, crime }, SalesTestKit.Location(), Array.Empty<string>(), random);

            Assert.IsNotNull(result);
            Assert.AreEqual("bC", result.Book.BookId);
            CollectionAssert.AreEqual(new[] { "Crime" }, result.MatchedGenres.ToArray());
        }

        [Test]
        public void SingleGenre_MultipleBooks_PicksByCumulativeWeight()
        {
            var b1 = new ShelfBook(Book("b1", "Fantasy", rarity: 1.0f));
            var b2 = new ShelfBook(Book("b2", "Fantasy", rarity: 3.0f));
            var shelf = new[] { b1, b2 };

            // Total weight = 4. roll = NextDouble * 4. b1 cumulative = 1, b2 cumulative = 4.
            // 0.1 * 4 = 0.4 < 1 → b1. 0.5 * 4 = 2.0 >= 1 → b2.
            var randomHitB1 = new FakeSalesRandom().EnqueueDouble(0.0, 0.1);
            var randomHitB2 = new FakeSalesRandom().EnqueueDouble(0.0, 0.5);

            Assert.AreEqual("b1", Sel(1.0).PickPassiveSale(shelf, SalesTestKit.Location(), Array.Empty<string>(), randomHitB1).Book.BookId);
            Assert.AreEqual("b2", Sel(1.0).PickPassiveSale(shelf, SalesTestKit.Location(), Array.Empty<string>(), randomHitB2).Book.BookId);
        }

        [Test]
        public void EmptyShelf_ReturnsNull()
        {
            var result = Sel(1.0).PickPassiveSale(
                Array.Empty<ShelfBook>(), SalesTestKit.Location(), Array.Empty<string>(), new FakeSalesRandom());
            Assert.IsNull(result);
        }

        [Test]
        public void OnlySoldOutBooks_ReturnsNull()
        {
            var sold = new ShelfBook(Book("b1", "Fantasy")) { State = ShelfBookState.SoldOut };

            var result = Sel(1.0).PickPassiveSale(
                new[] { sold }, SalesTestKit.Location(), Array.Empty<string>(), new FakeSalesRandom());

            Assert.IsNull(result);
        }

        [Test]
        public void BooksWithoutGenre_AreIgnored()
        {
            var noGenre = SalesTestKit.Book("bNoGenre");
            noGenre.Genre = null;
            var withGenre = Book("b1", "Fantasy");

            var shelf = new[] { new ShelfBook(noGenre), new ShelfBook(withGenre) };
            var random = new FakeSalesRandom().EnqueueDouble(0.0);

            var result = Sel(1.0).PickPassiveSale(shelf, SalesTestKit.Location(), Array.Empty<string>(), random);

            Assert.IsNotNull(result);
            Assert.AreEqual("b1", result.Book.BookId);
        }
    }
}
