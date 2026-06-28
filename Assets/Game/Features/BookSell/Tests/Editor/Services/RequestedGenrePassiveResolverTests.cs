using System;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class RequestedGenrePassiveResolverTests
    {
        private static Customer CustomerWith(params string[] genres)
            => new("c1", Array.Empty<ICustomerStep>(), new CustomerProfile(genres));

        private static CustomerContext Ctx(SalesShelf shelf, ISalesRandom random)
            => SalesTestKit.Context(shelf, SalesTestKit.Location(), new RecordingSink(), random: random);

        [Test]
        public void Hit_ReturnsChosenGenre_AndBookOfThatGenre()
        {
            var resolver = new RequestedGenrePassiveResolver(new FakeBaseSaleChanceCalculator(1.0));
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            var random = new FakeSalesRandom().EnqueueDouble(0.0); // gate roll < chance → hit
            var self = CustomerWith("sci-fi", "mystery");

            var result = resolver.Resolve(self, Ctx(shelf, random), shelf.AvailableForSelection());

            Assert.IsTrue(result.Success);
            Assert.AreEqual("sci-fi", result.ResolvedGenre);
            Assert.AreEqual("b1", result.Book.BookId);
        }

        [Test]
        public void GateMiss_ReturnsChosenGenre_NoBook()
        {
            var resolver = new RequestedGenrePassiveResolver(new FakeBaseSaleChanceCalculator(0.0));
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            var self = CustomerWith("sci-fi");

            var result = resolver.Resolve(self, Ctx(shelf, new FakeSalesRandom()), shelf.AvailableForSelection());

            Assert.IsFalse(result.Success);
            Assert.AreEqual("sci-fi", result.ResolvedGenre);
            Assert.IsNull(result.Book);
        }

        [Test]
        public void NoRequestedGenreOnShelf_MissesWithRequestedGenre()
        {
            var resolver = new RequestedGenrePassiveResolver(new FakeBaseSaleChanceCalculator(1.0));
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            var self = CustomerWith("romance"); // not stocked

            var result = resolver.Resolve(self, Ctx(shelf, new FakeSalesRandom()), shelf.AvailableForSelection());

            Assert.IsFalse(result.Success);
            Assert.AreEqual("romance", result.ResolvedGenre);
        }

        [Test]
        public void EmptyProfile_MissesWithNullGenre()
        {
            var resolver = new RequestedGenrePassiveResolver(new FakeBaseSaleChanceCalculator(1.0));
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            var self = new Customer("c1", Array.Empty<ICustomerStep>()); // empty profile

            var result = resolver.Resolve(self, Ctx(shelf, new FakeSalesRandom()), shelf.AvailableForSelection());

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.ResolvedGenre);
        }
    }
}
